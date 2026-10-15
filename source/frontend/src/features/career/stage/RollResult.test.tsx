import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { roll } from '../../../../test/fixtures';
import { RollResult } from './RollResult';

describe('roll result', () => {
  it('shows the roll against its threshold and the outcome in words', () => {
    render(<RollResult roll={roll()} onNext={vi.fn()} />);

    expect(screen.getByRole('heading', { name: 'Wynik: Sukces' })).toHaveFocus();
    expect(screen.getByText('Rzut 37 przy progu 58')).toBeInTheDocument();
  });

  it('says a guaranteed choice needed no roll and moves on', async () => {
    const onNext = vi.fn();
    render(
      <RollResult
        roll={roll({ choiceId: 'ignore', roll: null, threshold: 100 })}
        onNext={onNext}
      />,
    );

    expect(screen.getByText('Pewny wybór — bez rzutu')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Dalej' }));
    expect(onNext).toHaveBeenCalledOnce();
  });

  it('marks a failure in words, not only by colour', () => {
    render(<RollResult roll={roll({ roll: 91, outcome: 'criticalFailure' })} onNext={vi.fn()} />);

    expect(screen.getByRole('heading', { name: 'Wynik: Krytyczna porażka' })).toBeInTheDocument();
  });
});
