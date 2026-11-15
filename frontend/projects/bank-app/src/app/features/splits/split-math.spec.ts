import { evenShares, ownPart } from './split-math';

describe('split math', () => {
  it('splits evenly with the payer and leaves the odd kuruş to them', () => {
    expect(evenShares(100, 2)).toEqual([33.33, 33.33]);
    expect(ownPart(100, evenShares(100, 2))).toBe(33.34);
  });

  it('counts in kuruş, not in floating point', () => {
    expect(ownPart(0.3, [0.1, 0.2])).toBe(0);
    expect(ownPart(1250.5, [400.25, 400.25])).toBe(450);
  });

  it('has nothing to split without people or an amount', () => {
    expect(evenShares(100, 0)).toEqual([]);
    expect(evenShares(0, 3)).toEqual([]);
  });

  it('shows a negative own part when the shares exceed the total', () => {
    expect(ownPart(100, [60, 50])).toBe(-10);
  });
});
