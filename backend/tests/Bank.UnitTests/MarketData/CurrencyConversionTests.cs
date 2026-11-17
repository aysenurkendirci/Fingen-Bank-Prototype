using System.Net;
using System.Text;
using System.Text.Json;
using Bank.Application.Abstractions.MarketData;
using Bank.Application.Common.Exceptions;
using Bank.Application.Features.MarketData;
using Bank.Infrastructure.MarketData.VakifBank;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Bank.UnitTests.MarketData;

public sealed class CurrencyConversionTests
{
    private static readonly ExchangeRateSnapshot Rates = new(
        "TCMB", new DateOnly(2026, 11, 17), DateTime.UtcNow,
        [new ExchangeRate("USD", "ABD Doları", 40m, 40.2m), new ExchangeRate("EUR", "Euro", 44m, 44.3m)]);

    [Fact]
    public void BuyingACurrencyWithLira_IsAtTheSellingRate_RoundedDownToTheCent()
    {
        var quote = RateConversion.Convert(Rates, "TRY", 1_000m, "USD");

        // 1000 / 40.2 = 24.875..., and the customer never gets the part of a cent.
        Assert.Equal(24.87m, quote.TargetAmount);
        Assert.Equal(40.2m, quote.Rate);
    }

    [Fact]
    public void SellingACurrencyForLira_IsAtTheBuyingRate()
        => Assert.Equal(4_000m, RateConversion.Convert(Rates, "USD", 100m, "TRY").TargetAmount);

    [Fact]
    public void BetweenTwoForeignCurrencies_TheLiraSitsInTheMiddle()
        // 100 EUR sold for 4400 TRY, which buys 4400 / 40.2 = 109.45... USD.
        => Assert.Equal(109.45m, RateConversion.Convert(Rates, "EUR", 100m, "USD").TargetAmount);

    [Fact]
    public void ACurrencyWithoutARate_IsRefusedWithAMessage()
        => Assert.Throws<BadRequestException>(() => RateConversion.Convert(Rates, "TRY", 100m, "JPY"));

    [Fact]
    public void ThePortalsCalculatorExample_IsRead_WithTlAsTry()
    {
        const string example = """
            {
              "Header": { "StatusCode": "APIGW000000", "StatusDescription": "Completed", "ObjectID": "34600dc7-fe10-4394-a99a-36e28c9e3d3f" },
              "Data": { "Currency": { "RateDate": "2020-06-01T13:46:56.679Z", "SaleRate": "6.8466", "TargetCurrencyCode": "USD", "SaleAmount": "14.6" } }
            }
            """;

        var quote = VakifBankCurrencyCalculator.Parse(JsonDocument.Parse(example).RootElement, "TRY", 100m);

        Assert.Equal(("TRY", 100m, "USD", 14.6m, 6.8466m), (quote.SourceCurrency, quote.SourceAmount, quote.TargetCurrency, quote.TargetAmount, quote.Rate));
        Assert.Equal("VakıfBank", quote.Source);
    }

    [Fact]
    public async Task TheCalculator_SendsTheLiraAsTl_InPascalCase()
    {
        var gateway = new RecordingGateway();
        var options = Options.Create(new VakifBankOptions { ClientId = "client", ClientSecret = "secret" });
        var tokens = new VakifBankTokens(new SingleClientFactory(gateway), options, new FakeTimeProvider(DateTimeOffset.UtcNow));
        var calculator = new VakifBankCurrencyCalculator(new HttpClient(gateway) { BaseAddress = options.Value.BaseUrl }, tokens);

        await calculator.QuoteAsync("TRY", 100m, "USD", CancellationToken.None);

        var body = JsonDocument.Parse(gateway.CalculatorBody!).RootElement;
        Assert.Equal("TL", body.GetProperty("SourceCurrencyCode").GetString());
        Assert.Equal(100m, body.GetProperty("SourceAmount").GetDecimal());
        Assert.Equal("USD", body.GetProperty("TargetCurrencyCode").GetString());
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingGateway : HttpMessageHandler
    {
        public string? CalculatorBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/token"))
                return Json("""{ "access_token": "token", "expires_in": 3600 }""");

            CalculatorBody = await request.Content!.ReadAsStringAsync(ct);
            return Json("""{ "Header": { "StatusCode": "APIGW000000" }, "Data": { "Currency": { "SaleRate": 6.8466, "TargetCurrencyCode": "USD", "SaleAmount": 14.6 } } }""");
        }

        private static HttpResponseMessage Json(string json)
            => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
