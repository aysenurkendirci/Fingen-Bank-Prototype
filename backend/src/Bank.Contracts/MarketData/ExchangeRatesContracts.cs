namespace Bank.Contracts.MarketData;

/// <param name="Stale">True when the source could not be reached and the last known rates are shown instead.</param>
public sealed record ExchangeRatesResponse(
    string Source,
    DateOnly PublishedOn,
    DateTime FetchedAt,
    bool Stale,
    IReadOnlyList<ExchangeRateResponse> Rates
);

public sealed record ExchangeRateResponse(string CurrencyCode, string Name, decimal Buying, decimal Selling);
