import { describe, expect, it } from 'vitest';

import { planNext, stats } from '../../../test/fixtures';
import { actionBlock, optionFor, strongestCategory } from './plans';

describe('plans', () => {
  it('finds the option of an action, category and slot', () => {
    const { plans } = planNext((plan) => (plan.upgradeSlot === 'camera' ? 'unaffordable' : null));

    expect(optionFor(plans, 'improveProduction', 'chess', 'camera')).toMatchObject({
      disabledReason: 'unaffordable',
    });
    expect(optionFor(plans, 'rest', 'chess', null)).toMatchObject({
      plan: { mainAction: 'rest', category: 'chess' },
    });
  });

  it('blocks an action only when none of its options is legal', () => {
    const { plans } = planNext((plan) =>
      plan.mainAction === 'rest' || plan.upgradeSlot === 'camera' ? null : 'forced-rest',
    );

    expect(actionBlock(plans, 'rest', 'fps')).toBeNull();
    expect(actionBlock(plans, 'improveProduction', 'fps')).toBeNull();
    expect(actionBlock(plans, 'trainCategory', 'fps')).toBe('forced-rest');
  });

  it('starts on the strongest category', () => {
    expect(strongestCategory(stats())).toBe('fps');
  });
});
