import { accountLabel } from './account-display';

describe('accountLabel', () => {
  it('names a savings account by its goal', () => {
    expect(accountLabel({ accountType: 'Savings', goalName: 'İlk arabam' })).toBe('Kumbara: İlk arabam');
  });

  it('keeps the plain name for other accounts, and for a savings account without a goal', () => {
    expect(accountLabel({ accountType: 'Demand', goalName: null })).toBe('Vadesiz Hesap');
    expect(accountLabel({ accountType: 'Savings' })).toBe('Kumbara');
  });
});
