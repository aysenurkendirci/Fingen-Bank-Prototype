import { addDays, formatMonth, toIsoDate, todayIsoDate, upcomingWishListYear } from './dates';

describe('toIsoDate', () => {
  it('uses the local calendar date, not the UTC one', () => {
    // 00:30 local time; in any zone east of UTC, toISOString() would still say the previous day.
    const justAfterMidnight = new Date(2026, 3, 1, 0, 30);

    expect(toIsoDate(justAfterMidnight)).toBe('2026-04-01');
  });

  it('pads month and day', () => {
    expect(toIsoDate(new Date(2026, 0, 5))).toBe('2026-01-05');
  });
});

describe('todayIsoDate', () => {
  it("is the bank's day in Istanbul, not the device's", () => {
    // 21:30 UTC on 15 November is already 00:30 on 16 November in Istanbul (UTC+3).
    expect(todayIsoDate(new Date(Date.UTC(2026, 10, 15, 21, 30)))).toBe('2026-11-16');
    expect(todayIsoDate(new Date(Date.UTC(2026, 10, 15, 20, 30)))).toBe('2026-11-15');
  });
});

describe('addDays', () => {
  it('crosses months and years without any time zone shift', () => {
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
    expect(addDays('2028-02-28', 1)).toBe('2028-02-29');
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28');
  });
});

describe('upcomingWishListYear', () => {
  it('opens on the coming year from October, the current one before', () => {
    expect(upcomingWishListYear('2026-09-30')).toBe(2026);
    expect(upcomingWishListYear('2026-10-01')).toBe(2027);
    expect(upcomingWishListYear('2026-12-31')).toBe(2027);
    expect(upcomingWishListYear('2027-01-01')).toBe(2027);
  });
});

describe('formatMonth', () => {
  it('formats numeric and "yyyy-MM" months the same way', () => {
    expect(formatMonth({ year: 2026, month: 4 })).toBe(formatMonth('2026-04'));
  });

  it('uses Turkish month names', () => {
    expect(formatMonth('2026-04', 'long')).toBe('Nisan 2026');
  });
});
