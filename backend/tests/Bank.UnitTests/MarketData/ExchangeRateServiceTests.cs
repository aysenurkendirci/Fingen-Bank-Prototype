using Bank.Application.Abstractions.MarketData;
using Bank.Application.Common.Exceptions;
using Bank.Infrastructure.MarketData;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bank.UnitTests.MarketData;

public sealed class ExchangeRateServiceTests
{
    private static readonly ExchangeRateSnapshot Rates = new(
        "Test", new DateOnly(2026, 11, 13), new DateTime(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc),
        [new ExchangeRate("USD", "ABD DOLARI", 50m, 50.1m)]);

    private sealed class Source(string name, Func<ExchangeRateSnapshot> answer) : IExchangeRateSource
    {
        public int Calls { get; private set; }
        public string Name => name;

        public Task<ExchangeRateSnapshot> FetchAsync(CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(answer());
        }
    }

    private static Source Working(string name = "ok") => new(name, () => Rates with { Source = name });
    private static Source Broken(string name = "down") => new(name, () => throw new HttpRequestException("down"));

    private static ExchangeRateService Service(IMemoryCache cache, params IExchangeRateSource[] sources)
        => new(sources, cache, new ExchangeRateFetchGate(), Options.Create(new MarketDataOptions()), NullLogger<ExchangeRateService>.Instance);

    private static MemoryCache NewCache() => new(new MemoryCacheOptions());

    [Fact]
    public async Task TheFirstSourceThatAnswers_IsUsed()
    {
        var result = await Service(NewCache(), Broken(), Working("second")).GetAsync();

        Assert.Equal("second", result.Source);
        Assert.False(result.Stale);
    }

    [Fact]
    public async Task RatesAreCached_SoTheSourceIsAskedOnce()
    {
        var source = Working();
        var cache = NewCache();

        await Service(cache, source).GetAsync();
        await Service(cache, source).GetAsync();

        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task WhenEverySourceFails_TheLastGoodRatesAreServedAsStale()
    {
        var cache = NewCache();
        await Service(cache, Working()).GetAsync();
        cache.Remove("market:rates:fresh");

        var result = await Service(cache, Broken()).GetAsync();

        Assert.True(result.Stale);
        Assert.Equal(50m, result.Rates[0].Buying);
    }

    [Fact]
    public async Task WithNoSourceAndNothingCached_TheServiceIsUnavailable()
    {
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => Service(NewCache(), Broken()).GetAsync());
    }
}
