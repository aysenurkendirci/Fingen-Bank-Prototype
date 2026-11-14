namespace Bank.Application.Abstractions.MarketData;

/// <summary>
/// Today's exchange rates against the lira. Where they come from (TCMB, a bank's API) and how they are cached is
/// the infrastructure's business; the application only asks for the rates.
/// </summary>
public interface IExchangeRates
{
    /// <exception cref="Common.Exceptions.ServiceUnavailableException">No source answered and nothing is cached.</exception>
    Task<ExchangeRateSnapshot> GetAsync(CancellationToken ct = default);
}

/// <param name="Buying">What the publisher pays in lira for one unit.</param>
/// <param name="Selling">What one unit costs in lira.</param>
public sealed record ExchangeRate(string CurrencyCode, string Name, decimal Buying, decimal Selling);

/// <param name="Source">Who published the rates, shown next to them.</param>
/// <param name="PublishedOn">The business day the rates belong to; on a weekend that is the Friday.</param>
/// <param name="Stale">Every source failed just now, so these are the last rates that were fetched successfully.</param>
public sealed record ExchangeRateSnapshot(
    string Source,
    DateOnly PublishedOn,
    DateTime FetchedAt,
    IReadOnlyList<ExchangeRate> Rates,
    bool Stale = false);
