/** A calendar date as "yyyy-MM-dd", the format the API exchanges dates in. */
export type IsoDate = string;

/**
 * The user's local calendar date as "yyyy-MM-dd". `toISOString()` cannot be used: it converts to UTC
 * first, so between 00:00 and 03:00 in Turkey it returns yesterday's date.
 */
export function toIsoDate(date: Date): IsoDate {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

// "en-CA" writes dates as yyyy-MM-dd, which is exactly the API's format.
const istanbulDate = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit', day: '2-digit' });

/**
 * Today in the bank's calendar, Istanbul time, whatever the device's clock says. The API decides what is in the
 * past by the same calendar, so a customer abroad, or a browser on UTC just before midnight, never sends a date
 * the bank already calls yesterday.
 */
export function todayIsoDate(now: Date = new Date()): IsoDate {
  return istanbulDate.format(now);
}

const shortMonth = new Intl.DateTimeFormat('tr-TR', { month: 'short', year: 'numeric' });
const longMonth = new Intl.DateTimeFormat('tr-TR', { month: 'long', year: 'numeric' });

/** "Nis 2026" (short) or "Nisan 2026" (long) for a calendar month given as numbers or as "yyyy-MM". */
export function formatMonth(month: { year: number; month: number } | string, style: 'short' | 'long' = 'short'): string {
  const [year, monthNumber] = typeof month === 'string' ? month.split('-').map(Number) : [month.year, month.month];
  return (style === 'short' ? shortMonth : longMonth).format(new Date(year, monthNumber - 1, 1));
}
