import { getDefaultNormalizer, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { count, pln, signed } from '@/format';

import { roll, weekReport } from '../../../../test/fixtures';
import { WeekReport } from './WeekReport';

const items = (name: string) =>
  within(screen.getByRole('list', { name }))
    .getAllByRole('listitem')
    .map((item) => item.textContent);

describe('weekly report', () => {
  it('lists income, costs, cash change and stat changes with signs and causes', async () => {
    const onNext = vi.fn();
    render(<WeekReport report={weekReport(1)} onNext={onNext} />);

    expect(screen.getByRole('heading', { name: 'Raport — tydzień 1' })).toHaveFocus();
    expect(items('Przychody')).toEqual([`Suby ${signed(23, pln)}`, `Donejty ${signed(17, pln)}`]);
    expect(items('Koszty')).toEqual([`Życie ${signed(-100, pln)}`]);
    expect(items('Zmiany statystyk')).toEqual([
      'Energia +25 (akcja tygodnia)',
      'Umiejętności · FPS +0,8 (praktyka)',
      'Wiarygodność +2 (Rajd na czat)',
    ]);
    // Polish currency uses a no-break space; the default normalizer would turn the element's into a plain space.
    expect(
      screen.getByText(signed(-60, pln), {
        normalizer: getDefaultNormalizer({ collapseWhitespace: false }),
      }),
    ).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Następny tydzień' }));
    expect(onNext).toHaveBeenCalledOnce();
  });

  it('shows an unknown line by its code and the ending of the career', () => {
    render(
      <WeekReport
        report={weekReport(9, {
          income: [{ code: 'income:new-thing', amountPln: 5, delta: null, detail: null }],
          ending: 'insolvency',
        })}
      />,
    );

    expect(items('Przychody')).toEqual([`income:new-thing ${signed(5, pln)}`]);
    expect(screen.getByText('Bankructwo — kasa skończyła się na dobre.')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('lists the decision rolls and the networking roll with their thresholds', () => {
    render(
      <WeekReport
        report={weekReport(4, {
          decisionRolls: [
            roll({
              eventId: 'sponsor-offer',
              choiceId: 'sign',
              roll: 12,
              threshold: 50,
              eventTitle: 'Oferta sponsora',
              choiceLabel: 'Podpisz',
              outcomeText:
                '{gender, select, female {Podpisałaś} male {Podpisałeś} other {Podpisałoś}}.',
            }),
            roll({ eventId: 'sponsor-offer', choiceId: 'refuse', roll: null, threshold: 100 }),
          ],
          actionRoll: roll({
            eventId: 'network',
            choiceId: 'collaborate',
            roll: 77,
            threshold: 41,
            outcome: 'failure',
          }),
        })}
      />,
    );

    expect(items('Decyzje przed tygodniem')).toEqual([
      'Oferta sponsora: Podpisz — Sukces — Rzut 12 przy progu 50 Podpisałoś.',
      'sponsor-offer: refuse — Sukces — Pewny wybór — bez rzutu',
    ]);
    expect(screen.getByText('Networking', { selector: 'dt' }).nextElementSibling).toHaveTextContent(
      'Networking: collaborate — Porażka — Rzut 77 przy progu 41',
    );
  });

  it('shows no decision or networking lines for an M1 week', () => {
    render(<WeekReport report={weekReport(2)} />);

    expect(screen.queryByText('Decyzje przed tygodniem')).not.toBeInTheDocument();
    expect(screen.queryByText('Networking')).not.toBeInTheDocument();
    expect(screen.queryByText(/viralem/u)).not.toBeInTheDocument();
  });

  it('reports a viral clip apart from the live viewers and followers rows', () => {
    render(
      <WeekReport
        report={weekReport(5, {
          liveViewers: 1250,
          followersGained: 300,
          viral: { roll: 3, threshold: 5, multiplier: 2.5, followersBonus: 240 },
        })}
      />,
    );

    expect(
      screen.getByText('Klip poszedł viralem: widzowie ×2,50, +240 obserwujących (rzut 3 ≤ 5)'),
    ).toBeInTheDocument();
    expect(screen.getByText('Widzowie').nextElementSibling).toHaveTextContent(count(1250));
    expect(screen.getByText('Nowi obserwujący').nextElementSibling).toHaveTextContent('+300');
  });

  it('shows the follower change from events as a percentage', () => {
    render(
      <WeekReport
        report={weekReport(6, {
          statChanges: [
            { code: 'followers:event', amountPln: null, delta: 5, detail: null },
            { code: 'stat:Drama', amountPln: null, delta: 4, detail: 'network' },
            { code: 'stat:Credibility', amountPln: null, delta: -1, detail: 'jc-hot-take' },
          ],
        })}
      />,
    );

    expect(items('Zmiany statystyk')).toEqual([
      'Obserwujący (wydarzenia): +5%',
      'Drama +4 (networking)',
      'Wiarygodność −1 (wydarzenie)',
    ]);
  });
});
