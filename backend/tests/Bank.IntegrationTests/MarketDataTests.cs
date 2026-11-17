using System.Net;
using System.Net.Http.Json;
using Bank.Contracts.MarketData;
using Bank.IntegrationTests.Infrastructure;

namespace Bank.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class MarketDataTests
{
    private readonly ApiFactory _factory;

    public MarketDataTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ExchangeRates_AreServedToSignedInCustomers()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var rates = await client.GetFromJsonAsync<ExchangeRatesResponse>("/api/market/exchange-rates");

        Assert.Equal(FakeExchangeRateSource.Name, rates!.Source);
        Assert.Contains(rates.Rates, r => r.CurrencyCode == "USD" && r.Buying > 0);
    }

    [Fact]
    public async Task ExchangeRates_NeedASignIn()
    {
        var response = await _factory.CreateClient().GetAsync("/api/market/exchange-rates");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TheConverter_WorksFromTheRates_WhenNoBankCalculatorIsSetUp()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/market/convert", new ConvertCurrencyRequest("TRY", 1_000m, "USD"));
        var quote = await response.Content.ReadFromJsonAsync<ConversionResponse>();

        // The fake source sells a dollar for 40.20 lira: 1000 / 40.2, rounded down to the cent.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(24.87m, quote!.TargetAmount);
        Assert.Equal(FakeExchangeRateSource.Name, quote.Source);
    }

    [Theory]
    [InlineData("USD", "USD")]
    [InlineData("usd", "TRY")]
    public async Task AConversionWithTheSameOrAMalformedCurrency_Returns400(string from, string to)
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/market/convert", new ConvertCurrencyRequest(from, 100m, to));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
