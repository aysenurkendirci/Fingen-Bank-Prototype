using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Bank.Application.Abstractions.MarketData;

namespace Bank.Infrastructure.MarketData;

/// <summary>Reads TCMB's daily indicative rates file (kurlar/today.xml).</summary>
public static class TcmbRatesParser
{
    public const string SourceName = "TCMB";

    // The file is a few tens of kilobytes; anything far larger is not the file we asked for.
    private const long MaxCharacters = 2_000_000;

    /// <remarks>
    /// The document comes from outside, so it is read with DTDs prohibited and no resolver: no entity expansion
    /// (billion laughs) and no fetching of external resources (XXE).
    /// </remarks>
    public static ExchangeRateSnapshot Parse(Stream xml, DateTime fetchedAt)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxCharacters,
            IgnoreComments = true,
        };

        using var reader = XmlReader.Create(xml, settings);
        var root = XDocument.Load(reader).Root ?? throw new FormatException("TCMB rates file is empty.");

        var publishedOn = DateOnly.ParseExact(
            (string?)root.Attribute("Date") ?? throw new FormatException("TCMB rates file has no date."),
            "MM/dd/yyyy",
            CultureInfo.InvariantCulture);

        var rates = root.Elements("Currency")
            .Select(ToRate)
            .OfType<ExchangeRate>()
            .ToList();

        if (rates.Count == 0)
            throw new FormatException("TCMB rates file has no usable rates.");

        return new ExchangeRateSnapshot(SourceName, publishedOn, fetchedAt, rates);
    }

    // Some entries (SDR, a few currencies on some days) carry no forex rate; they are left out, not guessed.
    private static ExchangeRate? ToRate(XElement currency)
    {
        var code = (string?)currency.Attribute("CurrencyCode");
        if (code is not { Length: 3 } || !code.All(char.IsAsciiLetterUpper))
            return null;

        if (!TryDecimal(currency, "Unit", out var unit) || unit <= 0
            || !TryDecimal(currency, "ForexBuying", out var buying) || buying <= 0
            || !TryDecimal(currency, "ForexSelling", out var selling) || selling <= 0)
            return null;

        // Rates are quoted per Unit, e.g. 100 JPY; everything here is per one unit.
        var name = ((string?)currency.Element("Isim") ?? code).Trim();
        return new ExchangeRate(code, name, Math.Round(buying / unit, 6), Math.Round(selling / unit, 6));
    }

    private static bool TryDecimal(XElement parent, string name, out decimal value)
        => decimal.TryParse((string?)parent.Element(name), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}
