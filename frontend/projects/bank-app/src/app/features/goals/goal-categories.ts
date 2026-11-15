import { Goal, GoalCategory } from '../../data-access/models/savings.models';

export interface CategoryOption {
  readonly value: GoalCategory;
  readonly label: string;
  readonly icon: string;
  /** What the details field asks about for this kind of wish. */
  readonly hint: string;
}

export const GOAL_CATEGORIES: readonly CategoryOption[] = [
  { value: 'car', label: 'Araba', icon: 'directions_car', hint: 'Marka, model, yakıt türü, renk' },
  { value: 'home', label: 'Ev', icon: 'home', hint: 'Şehir, semt, oda sayısı, peşinat mı tamamı mı' },
  { value: 'travel', label: 'Seyahat', icon: 'flight', hint: 'Nereye, kaç kişi, ne zaman' },
  { value: 'education', label: 'Eğitim', icon: 'school', hint: 'Okul, kurs ya da sertifika, süresi' },
  { value: 'tech', label: 'Teknoloji', icon: 'devices', hint: 'Cihaz, model, kapasite' },
  { value: 'wedding', label: 'Düğün', icon: 'favorite', hint: 'Tarih, kaç kişilik, nerede' },
  { value: 'other', label: 'Diğer', icon: 'star', hint: 'Ne istediğinizi kendi sözlerinizle yazın' },
];

export function categoryOf(value: GoalCategory | null): CategoryOption | null {
  return GOAL_CATEGORIES.find((c) => c.value === value) ?? null;
}

/** How many years a wish spans: from its new year to the end of its target year. Null if either is missing. */
export function horizonYears(goal: Pick<Goal, 'newYearOf' | 'targetDate'>): number | null {
  if (!goal.newYearOf || !goal.targetDate) return null;
  const years = Number(goal.targetDate.slice(0, 4)) - goal.newYearOf + 1;
  return years >= 1 ? years : null;
}

/** The last day of the horizon: a 3-year wish for 2027 runs to the end of 2029. */
export function horizonEnd(year: number, years: number): string {
  return `${year + years - 1}-12-31`;
}
