// Expenses split with friends; mirrors Bank.Contracts.Splits.
import { IsoDate } from '../../core/util/dates';

export type ShareStatus = 'Open' | 'Paid' | 'Declined' | 'Cancelled';

export interface ShareRequestBody {
  name: string;
  /** A friend who banks here; without it the friend is a name and pays outside the app. */
  iban: string | null;
  amount: number;
}

export interface SplitRequestBody {
  title: string;
  totalAmount: number;
  spentOn: IsoDate;
  receivingAccountId: number;
  shares: ShareRequestBody[];
}

export interface Share {
  id: number;
  name: string;
  amount: number;
  status: ShareStatus;
  /** True for a friend who banks here and pays in the app. */
  inApp: boolean;
  settledAt: string | null;
}

export interface Split {
  id: number;
  title: string;
  totalAmount: number;
  currencyCode: string;
  spentOn: IsoDate;
  receivingIban: string;
  /** The payer's own part: the total less the others' shares. */
  ownShare: number;
  collected: number;
  status: 'Open' | 'Cancelled';
  createdAt: string;
  shares: Share[];
}

/** A share someone else asked of the signed-in customer. */
export interface ShareAsked {
  shareId: number;
  title: string;
  ownerName: string;
  amount: number;
  currencyCode: string;
  receivingIban: string;
  spentOn: IsoDate;
  status: ShareStatus;
  createdAt: string;
}
