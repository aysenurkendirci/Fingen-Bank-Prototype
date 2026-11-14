using Bank.Application.Abstractions.MarketData;

namespace Bank.Infrastructure.MarketData;

/// <summary>
/// The Central Bank's daily indicative rates. Public and keyless, so the app shows real rates even where no bank
/// API is configured.
/// </summary>
public sealed class TcmbExchangeRateSource : IExchangeRateSource
{
    private readonly HttpClient _http;
    private readonly TimeProvider _time;

    public TcmbExchangeRateSource(HttpClient http, TimeProvider time)
    {
        _http = http;
        _time = time;
    }

    public string Name => TcmbRatesParser.SourceName;

    public async Task<ExchangeRateSnapshot> FetchAsync(CancellationToken ct)
    {
        using var response = await _http.GetAsync("kurlar/today.xml", HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync(ct);
        return TcmbRatesParser.Parse(body, _time.GetUtcNow().UtcDateTime);
    }
}
