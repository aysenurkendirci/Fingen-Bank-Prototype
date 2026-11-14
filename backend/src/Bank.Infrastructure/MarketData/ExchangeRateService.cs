using Bank.Application.Abstractions.MarketData;
using Bank.Application.Common.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bank.Infrastructure.MarketData;

public sealed class MarketDataOptions
{
    public const string Section = "MarketData";

    /// <summary>How long fetched rates are served before asking the source again.</summary>
    public int CacheMinutes { get; init; } = 10;
}

/// <summary>Lets only one request at a time go out for rates; the others wait and then read the cache.</summary>
public sealed class ExchangeRateFetchGate
{
    public SemaphoreSlim Lock { get; } = new(1, 1);
}

/// <summary>
/// Rates for the whole app from a short cache. On a miss the sources are tried in order; if all of them fail the
/// last good rates are served marked as stale, because slightly old rates for display beat an error page.
/// </summary>
public sealed class ExchangeRateService : IExchangeRates
{
    private const string FreshKey = "market:rates:fresh";
    private const string LastGoodKey = "market:rates:last-good";

    private readonly IEnumerable<IExchangeRateSource> _sources;
    private readonly IMemoryCache _cache;
    private readonly ExchangeRateFetchGate _gate;
    private readonly MarketDataOptions _options;
    private readonly ILogger<ExchangeRateService> _logger;

    public ExchangeRateService(
        IEnumerable<IExchangeRateSource> sources,
        IMemoryCache cache,
        ExchangeRateFetchGate gate,
        IOptions<MarketDataOptions> options,
        ILogger<ExchangeRateService> logger)
    {
        _sources = sources;
        _cache = cache;
        _gate = gate;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ExchangeRateSnapshot> GetAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(FreshKey, out ExchangeRateSnapshot? cached))
            return cached!;

        await _gate.Lock.WaitAsync(ct);
        try
        {
            // Another request may have filled the cache while this one waited.
            if (_cache.TryGetValue(FreshKey, out cached))
                return cached!;

            foreach (var source in _sources)
            {
                try
                {
                    var snapshot = await source.FetchAsync(ct);
                    _cache.Set(FreshKey, snapshot, TimeSpan.FromMinutes(_options.CacheMinutes));
                    _cache.Set(LastGoodKey, snapshot);
                    return snapshot;
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Exchange rate source {Source} failed", source.Name);
                }
            }
        }
        finally
        {
            _gate.Lock.Release();
        }

        if (_cache.TryGetValue(LastGoodKey, out ExchangeRateSnapshot? lastGood))
            return lastGood! with { Stale = true };

        throw new ServiceUnavailableException("Döviz kurları şu an alınamıyor. Lütfen biraz sonra tekrar deneyin.");
    }
}
