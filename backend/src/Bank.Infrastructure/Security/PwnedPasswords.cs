using System.Security.Cryptography;
using System.Text;
using Bank.Application.Abstractions.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;

namespace Bank.Infrastructure.Security;

/// <summary>
/// Checks a password against the Pwned Passwords list with k-anonymity: only the first five characters of the
/// password's SHA-1 leave the server, and the list answers with every hash that starts the same way. The password,
/// its full hash and anything about the customer stay here. Padding is asked for, so even the size of the answer
/// says nothing.
/// </summary>
public sealed class PwnedPasswords : IBreachedPasswords
{
    private readonly HttpClient _http;
    private readonly ILogger<PwnedPasswords> _logger;

    public PwnedPasswords(HttpClient http, ILogger<PwnedPasswords> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<bool?> IsBreachedAsync(string password, CancellationToken ct = default)
    {
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));
        var (prefix, suffix) = (hash[..5], hash[5..]);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"range/{prefix}");
            request.Headers.Add("Add-Padding", "true");
            using var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(ct);
            return Contains(body, suffix);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            // Never a reason to refuse a sign-up; the password rules still apply.
            _logger.LogWarning("Breached password check unavailable: {Error}", e.Message);
            return null;
        }
    }

    /// <summary>Each line is "SUFFIX:COUNT"; padding lines have a count of zero and are not breaches.</summary>
    internal static bool Contains(string body, string suffix)
    {
        foreach (var line in body.Split('\n'))
        {
            var separator = line.IndexOf(':');
            if (separator != 35 || !line.AsSpan(0, 35).Equals(suffix, StringComparison.OrdinalIgnoreCase))
                continue;
            return int.TryParse(line.AsSpan(separator + 1).Trim(), out var count) && count > 0;
        }
        return false;
    }
}

/// <summary>Used where the check is switched off, e.g. a machine without internet access.</summary>
public sealed class NoBreachedPasswordCheck : IBreachedPasswords
{
    public Task<bool?> IsBreachedAsync(string password, CancellationToken ct = default) => Task.FromResult<bool?>(null);
}

public static class BreachedPasswordsRegistration
{
    public static IServiceCollection AddBreachedPasswords(this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue("Security:BreachedPasswords:Enabled", true))
        {
            services.AddSingleton<IBreachedPasswords, NoBreachedPasswordCheck>();
            return services;
        }

        var baseUrl = new Uri(configuration["Security:BreachedPasswords:BaseUrl"] ?? "https://api.pwnedpasswords.com/");
        services.AddHttpClient<IBreachedPasswords, PwnedPasswords>(client =>
            {
                client.BaseAddress = baseUrl;
                // A padded answer is around 30 KB.
                client.MaxResponseContentBufferSize = 256 * 1024;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("FinGen-PasswordCheck");
            })
            .AddStandardResilienceHandler(options =>
            {
                // Someone is waiting on the sign-up form; a slow answer is treated like no answer.
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(4);
                options.Retry.MaxRetryAttempts = 1;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
            });

        return services;
    }
}
