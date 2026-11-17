using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bank.Application.Abstractions.MarketData;

namespace Bank.Infrastructure.MarketData.VakifBank;

/// <summary>
/// Today's rates from VakıfBank's API Portal (POST /getCurrencyRates). Registered in front of TCMB only when the
/// client credentials are configured; when it fails, the rate service moves on to TCMB.
/// </summary>
public sealed class VakifBankExchangeRateSource : IExchangeRateSource
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    private readonly HttpClient _http;
    private readonly VakifBankTokens _tokens;
    private readonly TimeProvider _time;

    public VakifBankExchangeRateSource(HttpClient http, VakifBankTokens tokens, TimeProvider time)
    {
        _http = http;
        _tokens = tokens;
        _time = time;
    }

    public string Name => VakifBankRatesParser.SourceName;

    public async Task<ExchangeRateSnapshot> FetchAsync(CancellationToken ct)
    {
        using var response = await SendAsync(await _tokens.GetAsync(ct), ct);

        // A token the gateway no longer accepts (revoked, clock skew) is renewed once.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _tokens.Forget();
            using var retried = await SendAsync(await _tokens.GetAsync(ct), ct);
            return await ReadAsync(retried, ct);
        }

        return await ReadAsync(response, ct);
    }

    private Task<HttpResponseMessage> SendAsync(string token, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "getCurrencyRates")
        {
            // The gateway expects the property in PascalCase; JsonContent would camel-case an anonymous object.
            Content = JsonContent.Create(new RatesRequest(StartOfTodayInIstanbul())),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _http.SendAsync(request, ct);
    }

    private async Task<ExchangeRateSnapshot> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(body, cancellationToken: ct);
        return VakifBankRatesParser.Parse(json.RootElement, _time.GetUtcNow().UtcDateTime);
    }

    private sealed record RatesRequest([property: JsonPropertyName("ValidityDate")] string ValidityDate);

    // The service takes the day as the UTC instant of its Istanbul midnight: 1 June is "2020-05-31T21:00:00.000Z".
    private string StartOfTodayInIstanbul()
    {
        var local = TimeZoneInfo.ConvertTime(_time.GetUtcNow(), Istanbul);
        var midnight = new DateTimeOffset(local.Date, local.Offset);
        return midnight.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    }
}
