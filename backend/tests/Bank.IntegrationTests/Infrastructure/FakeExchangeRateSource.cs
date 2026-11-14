using Bank.Application.Abstractions.MarketData;
using Bank.Infrastructure.MarketData;

namespace Bank.IntegrationTests.Infrastructure;

/// <summary>Stands in for TCMB and bank APIs, so the suite never depends on the internet.</summary>
public sealed class FakeExchangeRateSource : IExchangeRateSource
{
    public const string Name = "Test kurları";

    string IExchangeRateSource.Name => Name;

    public Task<ExchangeRateSnapshot> FetchAsync(CancellationToken ct)
        => Task.FromResult(new ExchangeRateSnapshot(
            Name,
            DateOnly.FromDateTime(ApiFactory.Now.UtcDateTime),
            ApiFactory.Now.UtcDateTime,
            [new ExchangeRate("USD", "ABD DOLARI", 40m, 40.2m), new ExchangeRate("EUR", "EURO", 44m, 44.3m)]));
}
