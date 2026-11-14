/** A closed goal's savings account has nothing left to show; it stays in the ledger, not in the lists. */
export function hideClosedSavings<T extends { accountType: string; status: string }>(accounts: readonly T[]): T[] {
  return accounts.filter((a) => !(a.accountType === 'Savings' && a.status === 'Closed'));
}
