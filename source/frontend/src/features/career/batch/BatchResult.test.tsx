import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { BatchStopReason } from '@/api/generated/game.schemas';

import { batchOutcome } from '../../../../test/fixtures';
import { BatchResult } from './BatchResult';

describe('batch result', () => {
  it.each([
    [BatchStopReason.completed, 'Wszystkie tygodnie rozegrane.'],
    [BatchStopReason.pausedForDecision, 'Gra czeka na twoją decyzję.'],
    [BatchStopReason.careerEnded, 'Kariera zakończyła się w trakcie.'],
    [BatchStopReason.interrupted, 'Kariera zmieniła się w innej karcie — automat się zatrzymał.'],
  ])('explains why it stopped: %s', (stopReason, text) => {
    render(<BatchResult outcome={batchOutcome(stopReason)} onNext={vi.fn()} />);

    expect(screen.getByRole('heading', { name: 'Wynik automatycznych tygodni' })).toHaveFocus();
    expect(screen.getByText(text)).toBeInTheDocument();
  });

  it('summarises the batch and lists every week with its reasons', async () => {
    const onNext = vi.fn();
    render(<BatchResult outcome={batchOutcome()} onNext={onNext} />);

    const cash = within(screen.getByRole('table', { name: 'Podsumowanie' })).getByRole('row', {
      name: /Kasa/u,
    });
    // jest-dom turns the no-break space of Polish currency into a plain space before comparing.
    expect(cash).toHaveTextContent(/2000\s+zł\s*1880\s+zł/u);
    const weeks = within(screen.getByRole('list', { name: 'Tydzień po tygodniu' })).getAllByRole(
      'listitem',
      { name: /Tydzień/u },
    );
    expect(weeks).toHaveLength(2);
    expect(weeks[0]).toHaveTextContent('Odpoczynek · FPS');
    expect(weeks[0]).toHaveTextContent(
      'Energia 20 poniżej 30 — odpoczynek. Kategoria FPS: mocna strona.',
    );
    expect(weeks[0]).toHaveTextContent(
      'Rajd na czat: Przywitaj gości i odpal dla nich szybkie show — Sukces — Rzut 37 przy progu 58 Szansa 60% (próg 50%) — ryzyk-fizyk.',
    );
    expect(weeks[0]).toHaveTextContent('Szansa 60% (próg 50%) — ryzyk-fizyk.');
    await userEvent.click(screen.getByRole('button', { name: 'Dalej' }));
    expect(onNext).toHaveBeenCalledOnce();
  });

  it('nests the headings of a folded week report under its week heading', () => {
    const { container } = render(<BatchResult outcome={batchOutcome()} onNext={vi.fn()} />);

    const levels = [...container.querySelectorAll('h1, h2, h3, h4, h5, h6')].map((heading) =>
      Number(heading.tagName.slice(1)),
    );
    expect(levels).toContain(5);
    // Each heading is at most one level below the one before it.
    for (const [index, level] of levels.entries()) {
      expect(level - (levels[index - 1] ?? level)).toBeLessThanOrEqual(1);
    }
  });
});
