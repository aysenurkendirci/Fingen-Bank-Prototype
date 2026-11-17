using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Bank.Infrastructure.MarketData.VakifBank;

/// <summary>
/// An access token from VakıfBank's gateway with the client credentials grant, kept until shortly before it
/// expires. Only one request at a time asks for a new one. The secret travels only in the token request body,
/// over HTTPS, and is never logged. A singleton, so the token is shared; its HttpClient comes from the factory.
/// </summary>
public sealed class VakifBankTokens
{
    public const string HttpClientName = "VakifBank.Token";

    // A token is renewed this long before it runs out, so a request never leaves with one about to expire.
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(60);

    private readonly IHttpClientFactory _clients;
    private readonly VakifBankOptions _options;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresAt;

    public VakifBankTokens(IHttpClientFactory clients, IOptions<VakifBankOptions> options, TimeProvider time)
    {
        _clients = clients;
        _options = options.Value;
        _time = time;
    }

    public async Task<string> GetAsync(CancellationToken ct)
    {
        if (_token is { } valid && _time.GetUtcNow() < _expiresAt)
            return valid;

        await _gate.WaitAsync(ct);
        try
        {
            if (_token is { } renewed && _time.GetUtcNow() < _expiresAt)
                return renewed;

            using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = _options.ClientId!,
                    ["client_secret"] = _options.ClientSecret!,
                    ["scope"] = _options.Scope,
                }),
            };
            using var response = await _clients.CreateClient(HttpClientName).SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(ct)
                        ?? throw new FormatException("VakıfBank returned an empty token response.");
            if (string.IsNullOrWhiteSpace(token.AccessToken))
                throw new FormatException("VakıfBank returned no access token.");

            _token = token.AccessToken;
            _expiresAt = _time.GetUtcNow() + TimeSpan.FromSeconds(Math.Max(token.ExpiresIn, 0)) - Margin;
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Forgets the token, e.g. after the gateway refused it, so the next call asks for a new one.</summary>
    public void Forget() => _token = null;

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
