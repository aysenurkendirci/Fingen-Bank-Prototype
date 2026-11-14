// Market information; mirrors Bank.Contracts.MarketData.

export interface ExchangeRate {
  currencyCode: string;
  name: string;
  /** Lira paid for one unit. */
  buying: number;
  /** Lira charged for one unit. */
  selling: number;
}

export interface ExchangeRates {
  source: string;
  publishedOn: string;
  fetchedAt: string;
  /** The source was unreachable; these are the last rates fetched. */
  stale: boolean;
  rates: ExchangeRate[];
}
