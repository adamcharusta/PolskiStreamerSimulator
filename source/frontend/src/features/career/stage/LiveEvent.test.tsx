import { screen, waitFor } from '@testing-library/react';
import { delay, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { captureActions } from '../../../../test/api';
import { actionOutcome, careerView, eventNext, roll, settleNext } from '../../../../test/fixtures';
import { renderWithClient } from '../../../../test/render';
import { LiveEvent } from './LiveEvent';

const answer = () =>
  HttpResponse.json(
    actionOutcome(careerView({ version: 3, next: settleNext() }), { roll: roll() }),
  );

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
});
