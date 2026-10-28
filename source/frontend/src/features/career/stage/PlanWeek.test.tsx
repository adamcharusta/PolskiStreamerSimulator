import { screen, waitFor } from '@testing-library/react';
import { delay, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { captureActions } from '../../../../test/api';
import {
  actionOutcome,
  careerView,
  eventNext,
  networkOdds,
  planNext,
  weekReport,
} from '../../../../test/fixtures';
import { renderWithClient } from '../../../../test/render';
import { PlanWeek } from './PlanWeek';

const answer = () =>
  HttpResponse.json(actionOutcome(careerView({ version: 2, next: eventNext() })));

describe('week plan', () => {
  it('starts the chosen week with the version it was shown', async () => {
    const received = captureActions(answer);
    const { user } = renderWithClient(<PlanWeek career={careerView()} secret="secret-1" />);

    expect(screen.getByRole('heading', { name: 'Plan tygodnia' })).toHaveFocus();
    expect(screen.getByRole('radio', { name: 'FPS' })).toBeChecked();
    expect(screen.getByRole('button', { name: 'Zaczynam stream' })).toBeDisabled();
    await user.click(screen.getByRole('radio', { name: 'Odpoczynek' }));
    await user.click(screen.getByRole('button', { name: 'Zaczynam stream' }));

    await waitFor(() => {
      expect(received).toHaveLength(1);
    });
    expect(received[0]?.body).toMatchObject({
      expectedVersion: 1,
      type: 'startWeek',
      mainAction: 'rest',
      category: 'fps',
      upgradeSlot: null,
    });
  });

  it('explains why an action is disabled', () => {
    renderWithClient(
      <PlanWeek
        career={careerView({
          next: planNext((plan) => (plan.mainAction === 'rest' ? null : 'forced-rest')),
        })}
        secret="secret-1"
      />,
    );

    const train = screen.getByRole('radio', { name: /Trening/u });
    expect(train).toBeDisabled();
    expect(train).toHaveAccessibleDescription('Musisz odpocząć');
  });

  it('asks for a slot when improving production and blocks an unaffordable one', async () => {
    const received = captureActions(answer);
    const { user } = renderWithClient(
      <PlanWeek
        career={careerView({
          next: planNext((plan) => (plan.upgradeSlot === 'camera' ? 'unaffordable' : null)),
        })}
        secret="secret-1"
      />,
    );

    await user.click(screen.getByRole('radio', { name: 'Ulepszenie sprzętu' }));
    expect(screen.getByRole('button', { name: 'Zaczynam stream' })).toBeDisabled();
    expect(screen.getByRole('radio', { name: /Kamera/u })).toHaveAccessibleDescription(
      'Nie stać cię',
    );
    await user.click(screen.getByRole('radio', { name: /Mikrofon/u }));
    await user.click(screen.getByRole('button', { name: 'Zaczynam stream' }));

    await waitFor(() => {
      expect(received[0]?.body).toMatchObject({
        mainAction: 'improveProduction',
        upgradeSlot: 'microphone',
      });
    });
  });

  it('starts the week once on a double click', async () => {
    const received = captureActions(async () => {
      await delay(50);
      return answer();
    });
    const { user } = renderWithClient(<PlanWeek career={careerView()} secret="secret-1" />);

    await user.click(screen.getByRole('radio', { name: 'Odpoczynek' }));
    await user.dblClick(screen.getByRole('button', { name: 'Zaczynam stream' }));

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Zaczynam stream' })).not.toHaveAttribute(
        'aria-busy',
      );
    });
    expect(received).toHaveLength(1);
  });

  it('offers a retry with the same command id after a lost connection', async () => {
    const received = captureActions((_, call) => (call <= 3 ? HttpResponse.error() : answer()));
    const { user } = renderWithClient(<PlanWeek career={careerView()} secret="secret-1" />);

    await user.click(screen.getByRole('radio', { name: 'Odpoczynek' }));
    await user.click(screen.getByRole('button', { name: 'Zaczynam stream' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Wybór nie został zapisany — spróbuj ponownie.',
    );
    await user.click(screen.getByRole('button', { name: 'Spróbuj ponownie' }));

    await waitFor(() => {
      expect(received).toHaveLength(4);
    });
    expect(new Set(received.map((r) => r.body.commandId)).size).toBe(1);
  });

  it('locks the plan and moves focus to the retry after a lost connection', async () => {
    const received = captureActions((_, call) => (call <= 3 ? HttpResponse.error() : answer()));
    const { user } = renderWithClient(<PlanWeek career={careerView()} secret="secret-1" />);

    await user.click(screen.getByRole('radio', { name: 'Odpoczynek' }));
    await user.click(screen.getByRole('button', { name: 'Zaczynam stream' }));
    const retry = await screen.findByRole('button', { name: 'Spróbuj ponownie' });

    expect(retry).toHaveFocus();
    expect(screen.getByRole('radio', { name: 'Zwykły stream' })).toBeDisabled();
    expect(screen.getByRole('radio', { name: 'MOBA' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Zaczynam stream' })).toBeDisabled();
    await user.click(retry);

    await waitFor(() => {
      expect(received).toHaveLength(4);
    });
    expect(received[3]?.body).toMatchObject({ mainAction: 'rest' });
  });

  it('keeps the last report folded away', () => {
    renderWithClient(
      <PlanWeek
        career={careerView({ week: 4, lastSettledWeek: 3, lastReport: weekReport(3) })}
        secret="secret-1"
      />,
    );

    const summary = screen.getByText('Ostatni raport (tydzień 3)');
    expect(summary.closest('details')).not.toHaveAttribute('open');
  });

  it('offers only the actions the career has: no networking for an M1 career', () => {
    renderWithClient(<PlanWeek career={careerView()} secret="secret-1" />);

    expect(
      screen.queryByRole('radio', { name: /Networking i kolaboracje/u }),
    ).not.toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Odpoczynek' })).toBeInTheDocument();
  });

  it('shows the networking odds before the week starts', async () => {
    const received = captureActions(answer);
    const { user } = renderWithClient(
      <PlanWeek
        career={careerView({ rulesetId: 'm3-systems.1', next: planNext(undefined, networkOdds()) })}
        secret="secret-1"
      />,
    );

    expect(screen.queryByText('Szansa: 41%')).not.toBeInTheDocument();
    await user.click(screen.getByRole('radio', { name: 'Networking i kolaboracje' }));
    const odds = screen.getByRole('region', { name: 'Szansa networkingu' });
    expect(odds).toHaveTextContent('Szansa: 41%');
    expect(odds).toHaveTextContent('Wiarygodność +9 pp');
    expect(odds).toHaveTextContent('Drama −3 pp');
    expect(odds).toHaveTextContent('Możliwy wynik krytyczny');
    await user.click(screen.getByRole('button', { name: 'Zaczynam stream' }));

    await waitFor(() => {
      expect(received[0]?.body).toMatchObject({ mainAction: 'networkCollaborate' });
    });
  });

  it('explains a week that allows only rest', () => {
    renderWithClient(
      <PlanWeek
        career={careerView({
          rulesetId: 'm3-systems.1',
          next: planNext(
            (plan) => (plan.mainAction === 'rest' ? null : 'rest-only'),
            networkOdds(),
          ),
        })}
        secret="secret-1"
      />,
    );

    const network = screen.getByRole('radio', { name: /Networking/u });
    expect(network).toBeDisabled();
    expect(network).toHaveAccessibleDescription('Ten tydzień to odpoczynek');
  });
});
