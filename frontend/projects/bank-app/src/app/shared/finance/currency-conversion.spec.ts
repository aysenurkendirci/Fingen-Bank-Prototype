import { inLira, totalInLira } from './currency-conversion';

const rates = [
  { currencyCode: 'USD', name: 'ABD DOLARI', buying: 40, selling: 40.2 },
  { currencyCode: 'EUR', name: 'EURO', buying: 44, selling: 44.3 },
];

describe('currency conversion', () => {
  it('values foreign currency at the buying rate', () => {
    expect(inLira(100, 'USD', rates)).toBe(4000);
    expect(inLira(250, 'TRY', rates)).toBe(250);
  });

  it('says it does not know rather than guessing zero', () => {
    expect(inLira(100, 'CHF', rates)).toBeNull();
  });

  it('totals every balance, or nothing at all when a rate is missing', () => {
    expect(totalInLira([{ balance: 1000, currencyCode: 'TRY' }, { balance: 10, currencyCode: 'EUR' }], rates)).toBe(1440);
    expect(totalInLira([{ balance: 1000, currencyCode: 'TRY' }, { balance: 10, currencyCode: 'CHF' }], rates)).toBeNull();
  });
});
