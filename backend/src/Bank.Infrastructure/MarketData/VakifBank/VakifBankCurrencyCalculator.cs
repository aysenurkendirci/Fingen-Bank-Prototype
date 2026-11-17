using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bank.Application.Abstractions.MarketData;

namespace Bank.Infrastructure.MarketData.VakifBank;

/// <summary>
/// VakıfBank's own calculator (POST /currencyCalculator, Yatırım Servisleri, Döviz Hesaplayıcısı): the amount of
/// the target currency a source amount buys at the bank's selling rate, rounded the bank's way.
/// </summary>
/// <remarks>The service names the lira "TL" where this app uses ISO's "TRY"; the codes are translated both ways.</remarks>
public sealed class VakifBankCurrencyCalculator : ICurrencyCalculator
{
    private readonly HttpClient _http;
    private readonly VakifBankTokens _tokens;

    public VakifBankCurrencyCalculator(HttpClient http, VakifBankTokens tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    public string Name => VakifBankRatesParser.SourceName;

    public async Task<ConversionQuote> QuoteAsync(string sourceCurrency, decimal sourceAmount, string targetCurrency, CancellationToken ct)
    {
        var body = new CalculatorRequest(ToBank(sourceCurrency), sourceAmount, ToBank(targetCurrency));

        using var response = await SendAsync(body, await _tokens.GetAsync(ct), ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _tokens.Forget();
            using var retried = await SendAsync(body, await _tokens.GetAsync(ct), ct);
            return await ReadAsync(retried, sourceCurrency, sourceAmount, ct);
        }
        return await ReadAsync(response, sourceCurrency, sourceAmount, ct);
    }

    private Task<HttpResponseMessage> SendAsync(CalculatorRequest body, string token, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "currencyCalculator") { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _http.SendAsync(request, ct);
    }

    private static async Task<ConversionQuote> ReadAsync(HttpResponseMessage response, string sourceCurrency, decimal sourceAmount, CancellationToken ct)
    {
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return Parse(json.RootElement, sourceCurrency, sourceAmount);
    }

    public static ConversionQuote Parse(JsonElement root, string sourceCurrency, decimal sourceAmount)
    {
        var status = root.TryGetProperty("Header", out var header) && header.TryGetProperty("StatusCode", out var code) ? code.GetString() : null;
        if (status != VakifBankRatesParser.Completed)
            throw new FormatException($"VakıfBank calculator answered with status {status ?? "(none)"}.");
        if (!root.TryGetProperty("Data", out var data) || !data.TryGetProperty("Currency", out var currency) || currency.ValueKind != JsonValueKind.Object)
            throw new FormatException("VakıfBank calculator answer has no result.");

        var target = currency.TryGetProperty("TargetCurrencyCode", out var t) ? t.GetString() : null;
        if (string.IsNullOrEmpty(target) || !TryNumber(currency, "SaleAmount", out var amount) || !TryNumber(currency, "SaleRate", out var rate))
            throw new FormatException("VakıfBank calculator answer is incomplete.");

        DateTime? rateDate = currency.TryGetProperty("RateDate", out var d) && d.TryGetDateTimeOffset(out var at) ? at.UtcDateTime : null;
        return new ConversionQuote(sourceCurrency, sourceAmount, FromBank(target), amount, rate, rateDate, VakifBankRatesParser.SourceName);
    }

    private static string ToBank(string code) => code == "TRY" ? "TL" : code;

    private static string FromBank(string code) => code == "TL" ? "TRY" : code;

    private static bool TryNumber(JsonElement parent, string name, out decimal value)
    {
        value = 0;
        if (!parent.TryGetProperty(name, out var e))
            return false;
        var ok = e.ValueKind switch
        {
            JsonValueKind.Number => e.TryGetDecimal(out value),
            JsonValueKind.String => decimal.TryParse(e.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value),
            _ => false,
        };
        return ok && value > 0;
    }

    // PascalCase, as the portal documents it; JsonContent would otherwise camel-case the names.
    private sealed record CalculatorRequest(
        [property: JsonPropertyName("SourceCurrencyCode")] string SourceCurrencyCode,
        [property: JsonPropertyName("SourceAmount")] decimal SourceAmount,
        [property: JsonPropertyName("TargetCurrencyCode")] string TargetCurrencyCode);
}
