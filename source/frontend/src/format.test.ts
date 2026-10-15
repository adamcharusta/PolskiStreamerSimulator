import { describe, expect, it } from 'vitest';

import { count, multiplier, percent, pln, signed, stat } from './format';

describe('format', () => {
  it('writes negatives with a real minus sign and positives with a plus', () => {
    expect(signed(5)).toBe('+5');
    expect(signed(-3)).toBe('−3');
    expect(signed(0)).toBe('0');
    expect(signed(-60, pln)).toMatch(/^−60\szł$/u);
  });

  it('formats money, counts, percentages, stats and multipliers in Polish', () => {
    expect(pln(2000)).toMatch(/^2000\szł$/u);
    expect(count(12_345)).toMatch(/^12\s345$/u);
    expect(percent(0.58)).toBe('58%');
    expect(stat(18.82)).toBe('18,8');
    expect(multiplier(0.96)).toBe('0,96');
  });
});
