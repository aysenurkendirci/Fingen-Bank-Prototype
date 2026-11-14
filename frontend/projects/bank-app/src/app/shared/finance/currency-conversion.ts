import { ExchangeRate } from '../../data-access/models/market.models';

export interface Balance {
  balance: number;
  currencyCode: string;
}

/**
 * Lira value of foreign currency the customer holds, at the rate they would get selling it (the buying rate).
 * Null when there is no rate for the currency, so a missing rate is never mistaken for zero.
 */
export function inLira(amount: number, currencyCode: string, rates: readonly ExchangeRate[]): number | null {
  if (currencyCode === 'TRY') return amount;
  const rate = rates.find((r) => r.currencyCode === currencyCode);
  return rate ? Math.round(amount * rate.buying * 100) / 100 : null;
}

/** All balances in lira; null if any of them has no rate, since a partial total would mislead. */
export function totalInLira(balances: readonly Balance[], rates: readonly ExchangeRate[]): number | null {
  let total = 0;
  for (const { balance, currencyCode } of balances) {
    const value = inLira(balance, currencyCode, rates);
    if (value === null) return null;
    total += value;
  }
  return Math.round(total * 100) / 100;
}
