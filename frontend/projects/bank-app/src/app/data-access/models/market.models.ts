// Market information; mirrors Bank.Contracts.MarketData.

export interface ExchangeRate {
  currencyCode: string;
  name: string;
  /** Lira paid for one unit. */
  buying: number;
  /** Lira charged for one unit. */
  selling: number;
}

/** What an amount of one currency buys in another. TRY is the lira. */
export interface ConversionRequest {
  sourceCurrencyCode: string;
  sourceAmount: number;
  targetCurrencyCode: string;
}

export interface Conversion extends ConversionRequest {
  targetAmount: number;
  /** Lira per unit of the foreign currency the conversion used. */
  rate: number;
  rateDate: string | null;
  /** The bank's own calculator, or the source of the rates it was worked out from. */
  source: string;
}

export interface ExchangeRates {
  source: string;
  publishedOn: string;
  fetchedAt: string;
  /** The source was unreachable; these are the last rates fetched. */
  stale: boolean;
  rates: ExchangeRate[];
}
