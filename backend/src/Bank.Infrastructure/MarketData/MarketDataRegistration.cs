using Bank.Application.Abstractions.MarketData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bank.Infrastructure.MarketData.VakifBank;
using Microsoft.Extensions.Http.Resilience;

namespace Bank.Infrastructure.MarketData;

public static class MarketDataRegistration
{
    // A rates file is small; a response much larger than this is refused rather than read.
    private const long MaxResponseBytes = 1_000_000;

    /// <summary>
    /// Registers the rate sources in the order they are tried. A bank API is added in front of TCMB when its
    /// credentials are configured; without them TCMB alone answers.
    /// </summary>
    public static IServiceCollection AddMarketData(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MarketDataOptions>(configuration.GetSection(MarketDataOptions.Section));
        services.AddSingleton<ExchangeRateFetchGate>();
        services.AddTransient<IExchangeRates, ExchangeRateService>();

        // A bank's own rates come first when its credentials are set; TCMB answers when the bank does not.
        var vakifBank = configuration.GetSection(VakifBankOptions.Section).Get<VakifBankOptions>() ?? new VakifBankOptions();
        if (vakifBank.Configured)
        {
            services.Configure<VakifBankOptions>(configuration.GetSection(VakifBankOptions.Section));
            services.AddHttpClient(VakifBankTokens.HttpClientName, client => client.MaxResponseContentBufferSize = 64 * 1024)
                .AddStandardResilienceHandler(BankApiResilience);
            services.AddSingleton<VakifBankTokens>();
            services.AddHttpClient<VakifBankExchangeRateSource>(client =>
                {
                    client.BaseAddress = vakifBank.BaseUrl;
                    client.MaxResponseContentBufferSize = MaxResponseBytes;
                })
                .AddStandardResilienceHandler(BankApiResilience);
            services.AddTransient<IExchangeRateSource>(sp => sp.GetRequiredService<VakifBankExchangeRateSource>());
        }

        var tcmb = new Uri(configuration["MarketData:Tcmb:BaseUrl"] ?? "https://www.tcmb.gov.tr/");
        services.AddHttpClient<TcmbExchangeRateSource>(client =>
            {
                client.BaseAddress = tcmb;
                client.MaxResponseContentBufferSize = MaxResponseBytes;
            })
            .AddStandardResilienceHandler(Resilience);
        services.AddTransient<IExchangeRateSource>(sp => sp.GetRequiredService<TcmbExchangeRateSource>());

        return services;
    }

    // The bank goes first, so it gets less time than TCMB: a slow answer falls through to TCMB instead of
    // holding the rates up.
    private static void BankApiResilience(HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.Retry.MaxRetryAttempts = 1;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    }

    // TCMB sometimes takes close to ten seconds to answer, so an attempt gets that long. The page never waits for
    // this: rates load on their own request, and a cached or stale snapshot answers in the meantime.
    private static void Resilience(HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(25);
        options.Retry.MaxRetryAttempts = 1;
        // Must be at least twice the attempt timeout.
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    }
}
