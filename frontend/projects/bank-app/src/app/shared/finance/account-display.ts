import { AccountStatus, AccountType } from '../../data-access/models/dashboard.models';

// The API returns raw account codes; how they are labelled and drawn is a UI decision.
// Keyed by the union types so adding a new account type fails to compile until it is mapped here.

export const ACCOUNT_TYPE_DISPLAY: Record<AccountType, { label: string; icon: string }> = {
  Demand: { label: 'Vadesiz Hesap', icon: 'account_balance' },
  Time: { label: 'Vadeli Mevduat', icon: 'trending_up' },
  ForeignCurrency: { label: 'Döviz Hesabı', icon: 'wallet' },
  Savings: { label: 'Kumbara', icon: 'savings' },
};

/** What an account is called on screen: a savings account by its goal ("Kumbara: İlk arabam"). */
export function accountLabel(account: { accountType: AccountType; goalName?: string | null }): string {
  const label = ACCOUNT_TYPE_DISPLAY[account.accountType].label;
  return account.accountType === 'Savings' && account.goalName ? `${label}: ${account.goalName}` : label;
}

export const ACCOUNT_STATUS_LABELS: Record<AccountStatus, string> = {
  Active: 'Aktif',
  Frozen: 'Donduruldu',
  Closed: 'Kapalı',
};
