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
}
