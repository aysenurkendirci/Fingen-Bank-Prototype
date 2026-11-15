/**
 * An even split between the payer and the others, in kuruş, as the API does it: what does not divide evenly stays
 * with the payer, so nobody else pays more than an even share.
 */
export function evenShares(total: number, others: number): number[] {
  if (others <= 0 || !(total > 0)) return [];
  const each = Math.floor(Math.round(total * 100) / (others + 1)) / 100;
  return Array.from({ length: others }, () => each);
}

/** The payer's own part, in kuruş so that 0.1 + 0.2 never shows up as 0.30000000000000004. */
export function ownPart(total: number, shares: readonly number[]): number {
  const kurus = Math.round(total * 100) - shares.reduce((sum, s) => sum + Math.round((s || 0) * 100), 0);
  return kurus / 100;
}
