// Savings goals; mirrors Bank.Contracts.Savings.
import { IsoDate } from '../../core/util/dates';

export type AutoSaveMode = 'Fixed' | 'Percent';

export type GoalCategory = 'car' | 'home' | 'travel' | 'education' | 'tech' | 'wedding' | 'other';

export interface AutoSave {
  sourceAccountId: number;
  sourceIban: string;
  mode: AutoSaveMode;
  /** An amount for Fixed, a percentage for Percent. */
  value: number;
  /** Only money arriving in at least this amount triggers the rule. */
  minIncoming: number;
}

export interface Goal {
  id: number;
  name: string;
  targetAmount: number;
  savedAmount: number;
  targetDate: IsoDate | null;
  /** Set for a new year's resolution. */
  newYearOf: number | null;
  iban: string;
  monthlyNeeded: number | null;
  autoSave: AutoSave | null;
  category: GoalCategory | null;
  /** The wish in the customer's own words: the model, the colour, where to. */
  details: string | null;
  /** When the photo last changed; null without a photo. */
  photoUpdatedAt: string | null;
}

export interface GoalRequest {
  name: string;
  targetAmount: number;
  targetDate: IsoDate | null;
  newYearOf: number | null;
  category: GoalCategory | null;
  details: string | null;
}

export interface GoalMoneyRequest {
  accountId: number;
  amount: number;
}

export interface AutoSaveRequest {
  sourceAccountId: number;
  mode: AutoSaveMode;
  value: number;
  minIncoming: number;
}
