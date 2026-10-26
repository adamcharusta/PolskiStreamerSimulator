import { render, screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  CareerStatus,
  Category,
  CategoryPlan,
  EndingReason,
  MainAction,
  Platform,
  Reputation,
} from '@/api/generated/game.schemas';
import pl from '@/locales/pl/translation.json';

import { captureActions, captureBatches } from '../../../test/api';
import { actionOutcome, batchOutcome, careerView, endedNext } from '../../../test/fixtures';
import { renderWithClient } from '../../../test/render';
import { BatchLauncher } from './batch/BatchLauncher';
import {
  defaultStrategy,
  normalised,
  saveStrategy,
  storedStrategy,
  strategyErrors,
} from './batch/strategy';
import { EndedView } from './stage/EndedView';
import { RetireButton } from './stage/RetireButton';

vi.mock('@tanstack/react-router', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@tanstack/react-router')>()),
  Link: ({ children }: { children: ReactNode }) => <a href="/">{children}</a>,
}));

beforeEach(() => {
  localStorage.clear();
});

describe('strategy', () => {
  it('validates like the server', () => {
    expect(strategyErrors(defaultStrategy)).toEqual([]);
    expect(strategyErrors({ ...defaultStrategy, categoryPlan: CategoryPlan.focus })).toEqual([
      'focus-missing',
    ]);
    expect(strategyErrors({ ...defaultStrategy, routine: [] })).toEqual(['routine-length']);
    expect(
      strategyErrors({
        ...defaultStrategy,
        restBelowEnergy: 81,
        restAboveBurnout: 49,
        reservePln: -1,
      }),
    ).toEqual(['energy-range', 'burnout-range', 'reserve-range']);
  });

  it('drops a focus the plan does not use', () => {
    expect(normalised({ ...defaultStrategy, focus: Category.chess }).focus).toBeNull();
    expect(
      normalised({ ...defaultStrategy, categoryPlan: CategoryPlan.focus, focus: Category.chess })
        .focus,
    ).toBe(Category.chess);
  });

  it('remembers the strategy per career and ignores an invalid stored one', () => {
    saveStrategy('a', { ...defaultStrategy, reservePln: 500 });
    expect(storedStrategy('a').reservePln).toBe(500);
    expect(storedStrategy('b')).toEqual(defaultStrategy);
    localStorage.setItem('pss.strategy.c', JSON.stringify({ restBelowEnergy: 99 }));
    expect(storedStrategy('c')).toEqual(defaultStrategy);
    localStorage.setItem('pss.strategy.d', '{');
    expect(storedStrategy('d')).toEqual(defaultStrategy);
  });
});

describe('batch with a strategy', () => {
  it('sends the chosen length and strategy and remembers it', async () => {
    const received = captureBatches(() => HttpResponse.json(batchOutcome()));
    const { user } = renderWithClient(
      <BatchLauncher
        career={careerView({ rulesetId: 'm3-systems.2' })}
        secret="s"
        onDone={vi.fn()}
      />,
    );
    await user.click(screen.getByRole('button', { name: pl.batch.launch }));
    await user.click(screen.getByRole('radio', { name: '4 tygodnie' }));
    await user.click(screen.getByText(pl.strategy.title, { selector: 'summary' }));
    await user.selectOptions(screen.getByLabelText(pl.strategy.risk), 'bold');
    await user.click(screen.getByRole('button', { name: pl.strategy.removeStep }));
    await user.click(screen.getByRole('button', { name: 'Streamuj 4 tygodnie' }));
    await waitFor(() => {
      expect(received[0]?.body).toMatchObject({
        weeks: 4,
        strategy: {
          risk: 'bold',
          routine: [MainAction.noAction, MainAction.noAction, MainAction.noAction],
        },
      });
    });
    expect(storedStrategy(careerView().careerId).risk).toBe('bold');
  });

  it('blocks an invalid strategy with its error', async () => {
    const { user } = renderWithClient(
      <BatchLauncher career={careerView()} secret="s" onDone={vi.fn()} />,
    );
    await user.click(screen.getByRole('button', { name: pl.batch.launch }));
    await user.click(screen.getByText(pl.strategy.title, { selector: 'summary' }));
    await user.selectOptions(screen.getByLabelText(pl.strategy.categoryPlan), 'focus');
    expect(screen.getByRole('alert')).toHaveTextContent(pl.strategy.error['focus-missing']);
    expect(screen.getByRole('button', { name: 'Streamuj 13 tygodni' })).toBeDisabled();
    expect(screen.queryByLabelText(pl.strategy.wager)).toBeNull();
  });
});

const ended = () =>
  careerView({
    status: CareerStatus.ended,
    ending: EndingReason.completed,
    next: endedNext(),
    score: {
      followers: 0.8,
      wealth: 0.9,
      achievements: 0.7,
      followersPoints: 36_000,
      wealthPoints: 40_500,
      achievementPoints: 7000,
      baseScore: 83_500,
      difficultyMultiplier: 1.1,
      backgroundMultiplier: 1,
      finalScore: 91_850,
    },
    summary: {
      reason: EndingReason.completed,
      weeksPlayed: 1040,
      dominantCategory: Category.chess,
      reputation: Reputation.trusted,
      platform: Platform.nurt,
      bannedPlatforms: [Platform.czatnia],
      peakFollowersMilestone: 100_000,
      achievements: ['new-home'],
    },
    achievements: [
      {
        id: 'new-home',
        name: 'Nowy dom',
        description: 'Przeprowadzka z wyboru.',
        points: 2,
        earned: true,
      },
      { id: 'cash-1b', name: 'Miliarder', description: 'Miliard zł.', points: 1, earned: false },
    ],
  });

describe('ending', () => {
  it('shows the narrative, the score breakdown with both multipliers and the achievements', () => {
    render(<EndedView career={ended()} />);
    expect(
      screen.getByText(/1040 tygodni na wizji, najczęściej w kategorii Szachy/u),
    ).toBeVisible();
    expect(screen.getByText(/Bany po drodze: Czatnia/u)).toBeVisible();
    expect(screen.getByText(/Najważniejsze chwile: Nowy dom/u)).toBeVisible();
    const table = screen.getByRole('table', { name: pl.ending.score.title });
    expect(table).toHaveTextContent('91 850');
    expect(table).toHaveTextContent('×1,10');
    expect(table).toHaveTextContent('83 500');
    expect(screen.getByText(/✓ Nowy dom/u)).toBeVisible();
  });

  it('works for an M1 career without a score or names', () => {
    render(
      <EndedView
        career={careerView({
          status: CareerStatus.ended,
          ending: EndingReason.insolvency,
          next: endedNext(),
          achievements: [
            { id: 'followers-1k', name: null, description: null, points: 1, earned: false },
          ],
        })}
      />,
    );
    expect(screen.queryByRole('table')).toBeNull();
    expect(screen.getByText(/1000 obserwujących/u)).toBeVisible();
  });
});

describe('early retirement', () => {
  it('asks before ending the career', async () => {
    const received = captureActions(() =>
      HttpResponse.json(actionOutcome(careerView({ version: 2 }))),
    );
    const { user } = renderWithClient(<RetireButton career={careerView()} secret="s" />);
    await user.click(screen.getByRole('button', { name: pl.retire.action }));
    expect(screen.getByRole('alertdialog', { name: pl.retire.title })).toBeVisible();
    expect(received).toEqual([]);
    await user.click(screen.getByRole('button', { name: pl.retire.confirm }));
    await waitFor(() => {
      expect(received[0]?.body).toMatchObject({ type: 'retire', expectedVersion: 1 });
    });
  });
});
