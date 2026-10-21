import { screen, waitFor } from '@testing-library/react';
import { delay, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { PronounSet } from '@/api/generated/game.schemas';

import { captureActions } from '../../../../test/api';
import {
  actionOutcome,
  careerView,
  decisionNext,
  eventNext,
  planNext,
  roll,
  settleNext,
} from '../../../../test/fixtures';
import { renderWithClient } from '../../../../test/render';
import { LiveEvent } from './LiveEvent';

const answer = () =>
  HttpResponse.json(
    actionOutcome(careerView({ version: 3, next: settleNext() }), { roll: roll() }),
  );
const decision = (pronouns: PronounSet = PronounSet.woman) =>
  careerView({ rulesetId: 'm3-systems.1', version: 5, pronouns, next: decisionNext() });

describe('live event', () => {
  it('shows the odds, modifiers and critical chance of each choice', () => {
    renderWithClient(
      <LiveEvent career={careerView({ next: eventNext() })} secret="secret-1" onRolled={vi.fn()} />,
    );

    expect(screen.getByRole('heading', { name: 'Rajd na czat' })).toHaveFocus();
    const welcome = screen.getByRole('radio', { name: /Przywitaj gości/u }).closest('label');
    expect(welcome).toHaveTextContent('Szansa: 58%');
    expect(welcome).toHaveTextContent('Wiarygodność +3 pp');
    expect(welcome).toHaveTextContent('Możliwy wynik krytyczny');
    expect(screen.getByRole('radio', { name: /Graj dalej/u }).closest('label')).toHaveTextContent(
      'Szansa: pewne',
    );
    expect(screen.getByRole('button', { name: 'Wybieram' })).toBeDisabled();
  });

  it('resolves the chosen response and reveals the roll', async () => {
    const received = captureActions(answer);
    const onRolled = vi.fn();
    const { user } = renderWithClient(
      <LiveEvent
        career={careerView({ version: 2, next: eventNext() })}
        secret="secret-1"
        onRolled={onRolled}
      />,
    );

    await user.click(screen.getByRole('radio', { name: /Przywitaj gości/u }));
    await user.click(screen.getByRole('button', { name: 'Wybieram' }));

    await waitFor(() => {
      expect(onRolled).toHaveBeenCalledWith(roll());
    });
    expect(received[0]?.body).toMatchObject({
      expectedVersion: 2,
      type: 'resolveEvent',
      choiceId: 'welcome',
    });
  });

  it('locks the choices after a lost connection so the retry sends what is shown', async () => {
    captureActions(() => HttpResponse.error());
    const { user } = renderWithClient(
      <LiveEvent career={careerView({ next: eventNext() })} secret="secret-1" onRolled={vi.fn()} />,
    );

    await user.click(screen.getByRole('radio', { name: /Graj dalej/u }));
    await user.click(screen.getByRole('button', { name: 'Wybieram' }));

    expect(await screen.findByRole('button', { name: 'Spróbuj ponownie' })).toHaveFocus();
    expect(screen.getByRole('radio', { name: /Przywitaj gości/u })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Wybieram' })).toBeDisabled();
  });

  it('confirms a choice once on a double click', async () => {
    const received = captureActions(async () => {
      await delay(50);
      return answer();
    });
    const onRolled = vi.fn();
    const { user } = renderWithClient(
      <LiveEvent
        career={careerView({ next: eventNext() })}
        secret="secret-1"
        onRolled={onRolled}
      />,
    );

    await user.click(screen.getByRole('radio', { name: /Graj dalej/u }));
    await user.dblClick(screen.getByRole('button', { name: 'Wybieram' }));

    await waitFor(() => {
      expect(onRolled).toHaveBeenCalledOnce();
    });
    expect(received).toHaveLength(1);
  });

  it('marks a live situation as live', () => {
    renderWithClient(
      <LiveEvent career={careerView({ next: eventNext() })} secret="secret-1" onRolled={vi.fn()} />,
    );

    expect(screen.getByText(/Na żywo/u)).toBeInTheDocument();
    expect(screen.queryByText('Decyzja')).not.toBeInTheDocument();
  });

  it.each([
    [PronounSet.woman, 'Sponsorka pisze', 'Dostałaś ofertę współpracy.', /Podpisz jako twórczyni/u],
    [PronounSet.man, 'Sponsor pisze', 'Dostałeś ofertę współpracy.', /Podpisz jako twórca/u],
    [
      PronounSet.nonbinary,
      'Sponsorstwo pisze',
      'Dostałoś ofertę współpracy.',
      /Podpisz jako osoba twórcza/u,
    ],
  ])('formats an M3 event for the %s pronouns', (pronouns, title, text, label) => {
    renderWithClient(
      <LiveEvent career={decision(pronouns)} secret="secret-1" onRolled={vi.fn()} />,
    );

    expect(screen.getByRole('heading', { name: title })).toBeInTheDocument();
    expect(screen.getByText(text)).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: label })).toBeEnabled();
  });

  it('disables a choice the career cannot take and explains why', () => {
    renderWithClient(<LiveEvent career={decision()} secret="secret-1" onRolled={vi.fn()} />);

    const invest = screen.getByRole('radio', { name: /Kup lepszy sprzęt/u });
    expect(invest).toBeDisabled();
    expect(invest).toHaveAccessibleDescription('Brakuje pieniędzy');
  });

  it('shows the viral chance a critical success adds', () => {
    renderWithClient(<LiveEvent career={decision()} secret="secret-1" onRolled={vi.fn()} />);

    expect(screen.getByRole('radio', { name: /Podpisz/u }).closest('label')).toHaveTextContent(
      'kryt. sukces: +5 pp szansy na viral',
    );
    expect(screen.getByRole('radio', { name: /Odmów/u }).closest('label')).not.toHaveTextContent(
      'viral',
    );
  });

  it('answers a mandatory decision with its own action type', async () => {
    const received = captureActions(() =>
      HttpResponse.json(
        actionOutcome(careerView({ version: 6, next: planNext() }), {
          roll: roll({ eventId: 'sponsor-offer', choiceId: 'sign' }),
        }),
      ),
    );
    const onRolled = vi.fn();
    const { user } = renderWithClient(
      <LiveEvent career={decision()} secret="secret-1" onRolled={onRolled} />,
    );

    expect(screen.getByText('Decyzja')).toBeInTheDocument();
    expect(screen.queryByText(/Na żywo/u)).not.toBeInTheDocument();
    await user.click(screen.getByRole('radio', { name: /Podpisz/u }));
    await user.click(screen.getByRole('button', { name: 'Wybieram' }));

    await waitFor(() => {
      expect(onRolled).toHaveBeenCalledOnce();
    });
    expect(received[0]?.body).toMatchObject({
      expectedVersion: 5,
      type: 'resolveDecision',
      choiceId: 'sign',
    });
  });
});
