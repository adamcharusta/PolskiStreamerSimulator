import { getDefaultNormalizer, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { pln, signed } from '@/format';

import { weekReport } from '../../../../test/fixtures';
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
});
