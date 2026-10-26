import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { Category, MainAction, PronounSet } from '@/api/generated/game.schemas';

import { planNext, planOption, roll } from '../../../test/fixtures';
import { actionBlock } from './plans';
import { RollResult } from './stage/RollResult';

describe('roll result', () => {
  it('shows the event, the response and the authored outcome for the pronouns', () => {
    render(
      <RollResult
        roll={roll({
          eventTitle: 'Gorąca teza',
          choiceLabel: 'Broń tezy',
          outcomeText:
            '{gender, select, female {Wygrałaś} male {Wygrałeś} other {Wygrałoś}} debatę.',
        })}
        pronouns={PronounSet.woman}
        onNext={vi.fn()}
      />,
    );
    expect(screen.getByText('Gorąca teza: Broń tezy')).toBeVisible();
    expect(screen.getByText('Wygrałaś debatę.')).toBeVisible();
  });

  it('names a stored roll without texts by its translation or ID', () => {
    render(
      <RollResult roll={roll({ eventId: 'network', choiceId: 'collaborate' })} onNext={vi.fn()} />,
    );
    expect(screen.getByText('Networking')).toBeVisible();
  });
});

describe('blocked action', () => {
  it('names the most relevant reason of its options', () => {
    const plan = (slot: 'microphone' | 'camera') => ({
      mainAction: MainAction.improveProduction,
      category: Category.fps,
      trainTarget: null,
      upgradeSlot: slot,
      wager: null,
      targetPlatform: null,
    });
    const plans = [
      ...planNext().plans,
      planOption(plan('microphone'), 'max-tier'),
      planOption(plan('camera'), 'unaffordable'),
    ].filter(
      (o) => !(o.plan.mainAction === MainAction.improveProduction && o.disabledReason === null),
    );
    expect(actionBlock(plans, MainAction.improveProduction, Category.fps)).toBe('unaffordable');
  });
});
