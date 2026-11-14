using Bank.Application.Abstractions.MarketData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

    // Short timeouts: rates sit next to the customer's balance and must never keep the page waiting long.
    private static void Resilience(HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(4);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.Retry.MaxRetryAttempts = 2;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    }
}
