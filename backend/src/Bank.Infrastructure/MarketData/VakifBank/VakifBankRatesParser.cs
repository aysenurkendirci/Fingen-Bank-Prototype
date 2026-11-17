using System.Globalization;
using System.Text.Json;
using Bank.Application.Abstractions.MarketData;

namespace Bank.Infrastructure.MarketData.VakifBank;

/// <summary>Reads the answer of VakıfBank's /getCurrencyRates (Yatırım Servisleri, Döviz Kurları).</summary>
/// <remarks>
/// The bank quotes from the customer's side: SaleRate is what a customer gets for selling one unit to the bank,
/// PurchaseRate what a customer pays for buying one. In this app's terms the first is the bank's buying rate and
/// the second its selling rate. The documentation calls the rates numbers but the examples send strings; both
/// are accepted.
/// </remarks>
public static class VakifBankRatesParser
{
    public const string SourceName = "VakıfBank";

    /// <summary>The gateway's code for an answered request.</summary>
    public const string Completed = "APIGW000000";

    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public static ExchangeRateSnapshot Parse(JsonElement root, DateTime fetchedAt)
    {
        var status = root.TryGetProperty("Header", out var header) && header.TryGetProperty("StatusCode", out var code)
            ? code.GetString()
            : null;
        if (status != Completed)
            throw new FormatException($"VakıfBank answered with status {status ?? "(none)"}.");

        if (!root.TryGetProperty("Data", out var data) || !data.TryGetProperty("Currency", out var currencies)
            || currencies.ValueKind != JsonValueKind.Array)
            throw new FormatException("VakıfBank answer has no currency list.");

        var rates = new List<ExchangeRate>();
        DateOnly? publishedOn = null;
        foreach (var currency in currencies.EnumerateArray())
        {
            var currencyCode = currency.TryGetProperty("CurrencyCode", out var c) ? c.GetString() : null;
            if (currencyCode is not { Length: 3 } || !currencyCode.All(char.IsAsciiLetterUpper))
                continue;
            if (!TryRate(currency, "SaleRate", out var buying) || !TryRate(currency, "PurchaseRate", out var selling))
                continue;

            rates.Add(new ExchangeRate(currencyCode, CurrencyNames.Of(currencyCode), buying, selling));
            if (publishedOn is null && currency.TryGetProperty("RateDate", out var date) && date.TryGetDateTimeOffset(out var at))
                publishedOn = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, Istanbul).DateTime);
        }

        if (rates.Count == 0)
            throw new FormatException("VakıfBank answer has no usable rates.");

        return new ExchangeRateSnapshot(
            SourceName,
            publishedOn ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(fetchedAt, Istanbul)),
            fetchedAt,
            rates);
    }

    // A rate must be a positive number, sent as a number or as a string with a dot.
    private static bool TryRate(JsonElement currency, string name, out decimal value)
    {
        value = 0;
        if (!currency.TryGetProperty(name, out var element))
            return false;
        var parsed = element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetDecimal(out value),
            JsonValueKind.String => decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value),
            _ => false,
        };
        return parsed && value > 0;
    }
}

/// <summary>Turkish names of the currencies a bank quotes, for sources that send only the code.</summary>
public static class CurrencyNames
{
    private static readonly Dictionary<string, string> Names = new()
    {
        ["USD"] = "ABD Doları",
        ["EUR"] = "Euro",
        ["GBP"] = "İngiliz Sterlini",
        ["CHF"] = "İsviçre Frangı",
        ["JPY"] = "Japon Yeni",
        ["AUD"] = "Avustralya Doları",
        ["CAD"] = "Kanada Doları",
        ["DKK"] = "Danimarka Kronu",
        ["SEK"] = "İsveç Kronu",
        ["NOK"] = "Norveç Kronu",
        ["SAR"] = "Suudi Arabistan Riyali",
        ["RUB"] = "Rus Rublesi",
        ["CNY"] = "Çin Yuanı",
    };

    public static string Of(string code) => Names.GetValueOrDefault(code, code);
}
