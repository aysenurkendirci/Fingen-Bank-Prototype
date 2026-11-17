using System.Net;
using System.Text;
using System.Text.Json;
using Bank.Infrastructure.MarketData.VakifBank;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Bank.UnitTests.MarketData;

public sealed class VakifBankRatesTests
{
    // The example answer from the API Portal's "Döviz Kurları" page, as published.
    private const string PortalExample = """
        {
          "Header": { "StatusCode": "APIGW000000", "StatusDescription": "Completed", "ObjectID": "e0251ced-9ead-4086-8ec1-f46ac496a3d3" },
          "Data": {
            "Currency": [
              { "CurrencyCode": "EUR", "RateDate": "2020-05-31T21:00:00.000Z", "SaleRate": "7.58195", "PurchaseRate": "7.62595" },
              { "CurrencyCode": "DKK", "RateDate": "2020-05-31T21:00:00.000Z", "SaleRate": "0.9984", "PurchaseRate": "1.0384" },
              { "CurrencyCode": "RUB", "RateDate": "2020-05-31T21:00:00.000Z", "SaleRate": "0.09465", "PurchaseRate": "0.09865" },
              { "CurrencyCode": "USD", "RateDate": "2020-05-31T21:00:00.000Z", "SaleRate": "6.8086", "PurchaseRate": "6.8486" }
            ]
          }
        }
        """;

    private static readonly DateTimeOffset Now = new(2026, 11, 17, 21, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ThePortalExample_IsRead_WithTheBanksSaleRateAsItsBuyingRate()
    {
        var snapshot = Parse(PortalExample);
        var usd = snapshot.Rates.Single(r => r.CurrencyCode == "USD");

        Assert.Equal("VakıfBank", snapshot.Source);
        // 21:00 UTC on 31 May is midnight of 1 June in Istanbul.
        Assert.Equal(new DateOnly(2020, 6, 1), snapshot.PublishedOn);
        Assert.Equal(4, snapshot.Rates.Count);
        Assert.Equal(6.8086m, usd.Buying);
        Assert.Equal(6.8486m, usd.Selling);
        Assert.Equal("ABD Doları", usd.Name);
    }

    [Fact]
    public void RatesSentAsNumbers_AreReadToo()
    {
        var snapshot = Parse("""
            { "Header": { "StatusCode": "APIGW000000" },
              "Data": { "Currency": [ { "CurrencyCode": "EUR", "SaleRate": 36.1, "PurchaseRate": 36.9 } ] } }
            """);

        Assert.Equal(36.1m, snapshot.Rates.Single().Buying);
    }

    [Fact]
    public void AnAnswerThatIsNotCompleted_IsRefused_SoTcmbAnswersInstead()
        => Assert.Throws<FormatException>(() => Parse("""{ "Header": { "StatusCode": "APIGW000401" }, "Data": null }"""));

    [Theory]
    [InlineData("""{ "Header": { "StatusCode": "APIGW000000" }, "Data": { "Currency": [] } }""")]
    [InlineData("""{ "Header": { "StatusCode": "APIGW000000" }, "Data": { "Currency": [ { "CurrencyCode": "usd", "SaleRate": "1", "PurchaseRate": "1" } ] } }""")]
    [InlineData("""{ "Header": { "StatusCode": "APIGW000000" }, "Data": { "Currency": [ { "CurrencyCode": "USD", "SaleRate": "0", "PurchaseRate": "1" } ] } }""")]
    public void AnAnswerWithoutAUsableRate_IsRefused(string json)
        => Assert.Throws<FormatException>(() => Parse(json));

    [Fact]
    public async Task TheSource_AsksForATokenOnce_SendsTheIstanbulDay_AndRenewsARefusedToken()
    {
        var gateway = new FakeGateway(rateStatuses: [HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.OK]);
        var source = Source(gateway);

        await source.FetchAsync(CancellationToken.None);
        await source.FetchAsync(CancellationToken.None);

        // First fetch: one token, one rate call. Second: the cached token is refused, renewed once, and used again.
        Assert.Equal(2, gateway.TokenRequests.Count);
        Assert.Equal(3, gateway.RateBodies.Count);
        Assert.Contains("grant_type=client_credentials", gateway.TokenRequests[0]);
        Assert.Contains("scope=oob+public", gateway.TokenRequests[0]);
        // 21:30 UTC on 17 November is already the 18th in Istanbul; its midnight is 21:00 UTC on the 17th.
        Assert.Equal("2026-11-17T21:00:00.000Z", JsonDocument.Parse(gateway.RateBodies[0]).RootElement.GetProperty("ValidityDate").GetString());
        Assert.All(gateway.RateAuthorizations, a => Assert.StartsWith("Bearer token-", a));
    }

    private static Bank.Application.Abstractions.MarketData.ExchangeRateSnapshot Parse(string json)
        => VakifBankRatesParser.Parse(JsonDocument.Parse(json).RootElement, Now.UtcDateTime);

    private static VakifBankExchangeRateSource Source(FakeGateway gateway)
    {
        var time = new FakeTimeProvider(Now);
        var options = Options.Create(new VakifBankOptions { ClientId = "client", ClientSecret = "secret" });
        var tokens = new VakifBankTokens(new SingleClientFactory(gateway), options, time);
        return new VakifBankExchangeRateSource(new HttpClient(gateway) { BaseAddress = options.Value.BaseUrl }, tokens, time);
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    /// <summary>Answers token requests with a new token each time and rate requests with the given statuses in turn.</summary>
    private sealed class FakeGateway(HttpStatusCode[] rateStatuses) : HttpMessageHandler
    {
        private int _rateCalls;

        public List<string> TokenRequests { get; } = [];
        public List<string> RateBodies { get; } = [];
        public List<string> RateAuthorizations { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            if (request.RequestUri!.AbsolutePath.EndsWith("/token"))
            {
                TokenRequests.Add(body);
                return Json($$"""{ "access_token": "token-{{TokenRequests.Count}}", "expires_in": 3600 }""");
            }

            RateBodies.Add(body);
            RateAuthorizations.Add(request.Headers.Authorization!.ToString());
            var status = rateStatuses[_rateCalls++];
            return status == HttpStatusCode.OK ? Json(PortalExample) : new HttpResponseMessage(status);
        }

        private static HttpResponseMessage Json(string json)
            => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
