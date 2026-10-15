import { screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { captureBatches } from '../../../../test/api';
import { batchOutcome, careerView } from '../../../../test/fixtures';
import { renderWithClient } from '../../../../test/render';
import { BatchLauncher } from './BatchLauncher';

describe('batch launcher', () => {
  it('asks before running and sends 13 weeks at the shown version', async () => {
    const received = captureBatches(() => HttpResponse.json(batchOutcome()));
    const onDone = vi.fn();
    const { user } = renderWithClient(
      <BatchLauncher career={careerView({ version: 4 })} secret="secret-1" onDone={onDone} />,
    );

    await user.click(screen.getByRole('button', { name: 'Auto: 13 tygodni' }));
    expect(
      screen.getByRole('alertdialog', { name: 'Streamować automatycznie 13 tygodni?' }),
    ).toBeInTheDocument();
    expect(received).toEqual([]);
    await user.click(screen.getByRole('button', { name: 'Streamuj 13 tygodni' }));

    await waitFor(() => {
      expect(onDone).toHaveBeenCalledWith(batchOutcome());
    });
    expect(received[0]).toMatchObject({
      body: { expectedVersion: 4, weeks: 13 },
      authorization: 'Bearer secret-1',
    });
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('runs one batch on a double click and says it is running', async () => {
    // The batch answers only when the test releases it, so the running state is checked at any machine speed.
    let release: (() => void) | undefined;
    const released = new Promise<void>((resolve) => {
      release = resolve;
    });
    const received = captureBatches(async () => {
      await released;
      return HttpResponse.json(batchOutcome());
    });
    const onDone = vi.fn();
    const { user } = renderWithClient(
      <BatchLauncher career={careerView()} secret="secret-1" onDone={onDone} />,
    );

    await user.click(screen.getByRole('button', { name: 'Auto: 13 tygodni' }));
    await user.dblClick(screen.getByRole('button', { name: 'Streamuj 13 tygodni' }));

    expect(await screen.findByRole('status')).toHaveTextContent('Streamujesz 13 tygodni…');
    release?.();
    await waitFor(() => {
      expect(onDone).toHaveBeenCalledOnce();
    });
    expect(received).toHaveLength(1);
  });

  it('cancels without sending anything', async () => {
    const received = captureBatches(() => HttpResponse.json(batchOutcome()));
    const { user } = renderWithClient(
      <BatchLauncher career={careerView()} secret="secret-1" onDone={vi.fn()} />,
    );

    await user.click(screen.getByRole('button', { name: 'Auto: 13 tygodni' }));
    await user.click(screen.getByRole('button', { name: 'Anuluj' }));

    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(received).toEqual([]);
  });
});
