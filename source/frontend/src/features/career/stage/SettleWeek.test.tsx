import { screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { expect, it, vi } from 'vitest';

import { captureActions } from '../../../../test/api';
import { actionOutcome, careerView, settleNext, weekReport } from '../../../../test/fixtures';
import { renderWithClient } from '../../../../test/render';
import { SettleWeek } from './SettleWeek';

it('settles the week and reveals its report', async () => {
  const received = captureActions(() =>
    HttpResponse.json(
      actionOutcome(careerView({ version: 2, week: 2, lastSettledWeek: 1 }), {
        report: weekReport(1),
      }),
    ),
  );
  const onSettled = vi.fn();
  const { user } = renderWithClient(
    <SettleWeek
      career={careerView({ next: settleNext() })}
      secret="secret-1"
      onSettled={onSettled}
    />,
  );

  expect(screen.getByRole('heading', { name: 'Koniec streamu' })).toHaveFocus();
  await user.click(screen.getByRole('button', { name: 'Kończę tydzień' }));

  await waitFor(() => {
    expect(onSettled).toHaveBeenCalledWith(weekReport(1));
  });
  expect(received[0]?.body).toMatchObject({ expectedVersion: 1, type: 'settleWeek' });
});
