using System.Text;
using System.Xml;
using Bank.Infrastructure.MarketData;

namespace Bank.UnitTests.MarketData;

public sealed class TcmbRatesParserTests
{
    private static readonly DateTime FetchedAt = new(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);

    private const string File = """
        <?xml version="1.0" encoding="UTF-8"?>
        <?xml-stylesheet type="text/xsl" href="isokur.xsl"?>
        <Tarih_Date Tarih="13.11.2026" Date="11/13/2026" Bulten_No="2026/214">
          <Currency CrossOrder="0" Kod="USD" CurrencyCode="USD">
            <Unit>1</Unit><Isim>ABD DOLARI</Isim><ForexBuying>50.1926</ForexBuying><ForexSelling>50.2830</ForexSelling>
          </Currency>
          <Currency CrossOrder="3" Kod="JPY" CurrencyCode="JPY">
            <Unit>100</Unit><Isim>JAPON YENİ</Isim><ForexBuying>32.4500</ForexBuying><ForexSelling>32.6650</ForexSelling>
          </Currency>
          <Currency CrossOrder="18" Kod="XDR" CurrencyCode="XDR">
            <Unit>1</Unit><Isim>ÖZEL ÇEKME HAKKI (SDR)</Isim><ForexBuying/><ForexSelling/>
          </Currency>
        </Tarih_Date>
        """;

    private static Stream Xml(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    [Fact]
    public void ReadsTheBusinessDayAndRates()
    {
        var snapshot = TcmbRatesParser.Parse(Xml(File), FetchedAt);

        Assert.Equal(new DateOnly(2026, 11, 13), snapshot.PublishedOn);
        Assert.Equal("TCMB", snapshot.Source);
        var usd = Assert.Single(snapshot.Rates, r => r.CurrencyCode == "USD");
        Assert.Equal((50.1926m, 50.2830m, "ABD DOLARI"), (usd.Buying, usd.Selling, usd.Name));
    }

    [Fact]
    public void RatesQuotedPerHundred_AreTurnedIntoRatesPerOne()
    {
        var jpy = Assert.Single(TcmbRatesParser.Parse(Xml(File), FetchedAt).Rates, r => r.CurrencyCode == "JPY");

        Assert.Equal(0.3245m, jpy.Buying);
    }

    [Fact]
    public void EntriesWithoutAForexRate_AreLeftOut()
    {
        Assert.DoesNotContain(TcmbRatesParser.Parse(Xml(File), FetchedAt).Rates, r => r.CurrencyCode == "XDR");
    }

    [Fact]
    public void ADocumentType_IsRefused_SoEntitiesCannotBeExpanded()
    {
        const string hostile = """
            <?xml version="1.0"?>
            <!DOCTYPE r [<!ENTITY a "aaaaaaaaaa"><!ENTITY b "&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;">]>
            <Tarih_Date Date="11/13/2026"><Currency CurrencyCode="USD"><Unit>1</Unit><Isim>&b;</Isim></Currency></Tarih_Date>
            """;

        Assert.Throws<XmlException>(() => TcmbRatesParser.Parse(Xml(hostile), FetchedAt));
    }

    [Fact]
    public void AFileWithoutUsableRates_IsAnError()
    {
        const string empty = """<Tarih_Date Date="11/13/2026"></Tarih_Date>""";

        Assert.Throws<FormatException>(() => TcmbRatesParser.Parse(Xml(empty), FetchedAt));
    }
}
