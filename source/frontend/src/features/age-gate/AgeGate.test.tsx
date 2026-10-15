import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { renderApp, TEST_CAREER_ID } from '../../../test/render';
import { server } from '../../../test/server';

const gate = () =>
  screen.findByRole('alertdialog', { name: 'Gra tylko dla osób pełnoletnich (18+)' });

describe('the 18+ gate', () => {
  it('shows only the gate and its themes before confirmation', async () => {
    renderApp('/', { confirmed: false });

    expect(await gate()).toHaveTextContent('hazard za fikcyjną walutę gry');
    expect(screen.queryByText('Twoje kariery')).not.toBeInTheDocument();
  });

  it('does not open the game on Escape, an outside click or navigating back', async () => {
    const { user, history } = renderApp('/', { confirmed: false });
    await gate();

    await user.keyboard('{Escape}');
    const overlay = document.querySelector('.dialog-overlay');
    expect(overlay).not.toBeNull();
    await user.click(overlay as Element);
    history.push('/nowa-kariera');
    history.back();

    expect(await gate()).toBeInTheDocument();
    expect(localStorage.getItem('pss.ageGate.v1')).toBeNull();
  });

  it('shows a neutral exit when the player leaves', async () => {
    const { user } = renderApp('/', { confirmed: false });

    await user.click(await screen.findByRole('button', { name: 'Wychodzę' }));

    expect(screen.getByRole('heading', { name: 'Do zobaczenia' })).toBeInTheDocument();
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(screen.queryByText('Twoje kariery')).not.toBeInTheDocument();
    expect(localStorage.getItem('pss.ageGate.v1')).toBeNull();
  });

  it('opens the game after confirmation, remembers it and shows the online notice once', async () => {
    const { user } = renderApp('/', { confirmed: false });

    await user.click(
      await screen.findByRole('button', { name: 'Mam 18 lat lub więcej — wchodzę' }),
    );

    expect(await screen.findByText('Twoje kariery')).toBeInTheDocument();
    expect(localStorage.getItem('pss.ageGate.v1')).toBe('confirmed');
    await user.click(screen.getByRole('button', { name: 'Rozumiem' }));
    expect(screen.queryByText(/Gra wymaga połączenia/u)).not.toBeInTheDocument();
    expect(localStorage.getItem('pss.onlineNotice.v1')).toBe('dismissed');
  });

  it('shows the gate first on a deep link and makes no request', async () => {
    let requests = 0;
    server.use(
      http.all('*', () => {
        requests += 1;
        return HttpResponse.error();
      }),
    );

    renderApp(`/kariera/${TEST_CAREER_ID}`, { confirmed: false });

    expect(await gate()).toBeInTheDocument();
    expect(requests).toBe(0);
  });

  it('skips the gate once it was confirmed', async () => {
    renderApp('/');

    expect(await screen.findByText('Twoje kariery')).toBeInTheDocument();
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('keeps the gate usable when browser storage is unavailable', async () => {
    const getItem = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('Storage disabled', 'SecurityError');
    });

    const { user } = renderApp('/', { confirmed: false });
    expect(await gate()).toBeInTheDocument();
    getItem.mockRestore();

    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('Storage disabled', 'SecurityError');
    });
    try {
      await user.click(screen.getByRole('button', { name: 'Mam 18 lat lub więcej — wchodzę' }));
      expect(await screen.findByText('Twoje kariery')).toBeInTheDocument();
    } finally {
      setItem.mockRestore();
    }
  });
});
