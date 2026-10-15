import { onlineManager } from '@tanstack/react-query';
import { screen, waitFor, within } from '@testing-library/react';
import { delay, http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { knownCareers } from '@/careers/knownCareers';

import { captureActions, problem, serveCareer } from '../../../test/api';
import {
  actionOutcome,
  careerView,
  endedNext,
  eventNext,
  roll,
  settleNext,
  stats,
  weekReport,
} from '../../../test/fixtures';
import { rememberTestCareer, renderApp, TEST_CAREER_ID } from '../../../test/render';
import { server } from '../../../test/server';

const path = `/kariera/${TEST_CAREER_ID}`;
const desk = () => screen.getByRole('region', { name: 'Stan kariery' });

describe('game screen', () => {
  it('plays a full manual week through the stage', async () => {
    rememberTestCareer();
    serveCareer(careerView());
    captureActions((body) => {
      switch (body.type) {
        case 'startWeek': {
          return HttpResponse.json(actionOutcome(careerView({ version: 2, next: eventNext() })));
        }
        case 'resolveEvent': {
          return HttpResponse.json(
            actionOutcome(careerView({ version: 3, next: settleNext() }), { roll: roll() }),
          );
        }
        default: {
          return HttpResponse.json(
            actionOutcome(
              careerView({ version: 4, week: 2, lastSettledWeek: 1, lastReport: weekReport(1) }),
              { report: weekReport(1) },
            ),
          );
        }
      }
    });
    const { user } = renderApp(path);

    await user.click(await screen.findByRole('radio', { name: 'Odpoczynek' }));
    await user.click(screen.getByRole('button', { name: 'Zaczynam stream' }));
    expect(await screen.findByRole('heading', { name: 'Rajd na czat' })).toHaveFocus();
    await user.click(screen.getByRole('radio', { name: /Przywitaj gości/u }));
    await user.click(screen.getByRole('button', { name: 'Wybieram' }));
    expect(await screen.findByRole('heading', { name: 'Wynik: Sukces' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Dalej' }));
    await user.click(await screen.findByRole('button', { name: 'Kończę tydzień' }));
    expect(await screen.findByRole('heading', { name: 'Raport — tydzień 1' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Następny tydzień' }));

    expect(await screen.findByRole('heading', { name: 'Plan tygodnia' })).toBeInTheDocument();
    expect(desk()).toHaveTextContent('Tydzień 2');
    expect(desk()).toHaveTextContent('Zapisano');
  });

  it('shows the server-confirmed state and the last report after a reload', async () => {
    rememberTestCareer();
    const seen = serveCareer(
      careerView({
        week: 4,
        lastSettledWeek: 3,
        lastReport: weekReport(3),
        stats: stats({ cashPln: 2500 }),
      }),
    );

    renderApp(path);

    expect(await screen.findByText('Ostatni raport (tydzień 3)')).toBeInTheDocument();
    expect(desk()).toHaveTextContent('Tydzień 4');
    // jest-dom turns the no-break space of Polish currency into a plain space before comparing.
    expect(desk()).toHaveTextContent(/Kasa: 2500\s+zł/u);
    expect(seen[0]).toBe('Bearer secret-1');
  });

  it('refetches the career after a stale version and says why', async () => {
    rememberTestCareer();
    serveCareer(careerView(), careerView({ version: 5, next: eventNext() }));
    captureActions(() => problem(409, 'stale-version'));
    const { user } = renderApp(path);

    await user.click(await screen.findByRole('radio', { name: 'Odpoczynek' }));
    await user.click(screen.getByRole('button', { name: 'Zaczynam stream' }));

    expect(await screen.findByRole('heading', { name: 'Rajd na czat' })).toBeInTheDocument();
    expect(
      screen.getByText('Kariera zmieniła się w innej karcie. Pokazuję aktualny stan.'),
    ).toBeInTheDocument();
  });

  it('moves focus to the fresh plan after another tab moved the career', async () => {
    rememberTestCareer();
    serveCareer(careerView(), careerView({ version: 5 }));
    captureActions(() => problem(409, 'stale-version'));
    const { user } = renderApp(path);

    await user.click(await screen.findByRole('radio', { name: 'Odpoczynek' }));
    await user.click(screen.getByRole('button', { name: 'Zaczynam stream' }));

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'Plan tygodnia' })).toHaveFocus();
    });
    expect(screen.getByRole('radio', { name: 'Odpoczynek' })).not.toBeChecked();
  });

  it('says the connection is missing when the browser is offline', async () => {
    rememberTestCareer();
    serveCareer(careerView());
    captureActions(() => HttpResponse.error());
    const { user } = renderApp(path);
    await user.click(await screen.findByRole('radio', { name: 'Odpoczynek' }));

    onlineManager.setOnline(false);
    try {
      await user.click(screen.getByRole('button', { name: 'Zaczynam stream' }));

      expect(await screen.findByRole('button', { name: 'Spróbuj ponownie' })).toBeInTheDocument();
      expect(within(desk()).getByRole('status')).toHaveTextContent('Brak połączenia');
    } finally {
      onlineManager.setOnline(true);
    }
  });

  it('offers a reload when the career is opened while the browser is offline', async () => {
    rememberTestCareer();
    server.use(http.get('/v1/careers/:id', () => HttpResponse.error()));
    onlineManager.setOnline(false);
    try {
      renderApp(path);

      expect(await screen.findByRole('alert')).toHaveTextContent('Nie udało się wczytać kariery.');
    } finally {
      onlineManager.setOnline(true);
    }
  });

  it('shows the saving state while a step is pending', async () => {
    rememberTestCareer();
    serveCareer(careerView());
    captureActions(async () => {
      await delay(50);
      return HttpResponse.json(actionOutcome(careerView({ version: 2, next: eventNext() })));
    });
    const { user } = renderApp(path);

    await user.click(await screen.findByRole('radio', { name: 'Odpoczynek' }));
    await user.click(screen.getByRole('button', { name: 'Zaczynam stream' }));

    expect(within(desk()).getByRole('status')).toHaveTextContent('Zapisuję…');
    await waitFor(() => {
      expect(within(desk()).getByRole('status')).toHaveTextContent('Zapisano');
    });
  });

  it('can forget a career the server does not find', async () => {
    rememberTestCareer();
    server.use(http.get('/v1/careers/:id', () => problem(404, 'career-not-found')));
    const { user, router } = renderApp(path);

    await user.click(await screen.findByRole('button', { name: 'Usuń z listy' }));

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/');
    });
    expect(knownCareers()).toEqual([]);
  });

  it('shows not found when this browser has no secret for the career', async () => {
    renderApp(path);

    expect(
      await screen.findByRole('heading', { name: 'Nie znaleziono kariery' }),
    ).toBeInTheDocument();
  });

  it('offers a reload when the career cannot be loaded', async () => {
    rememberTestCareer();
    let call = 0;
    server.use(
      http.get('/v1/careers/:id', () => {
        call += 1;
        return call === 1 ? problem(500, 'internal-error') : HttpResponse.json(careerView());
      }),
    );
    const { user } = renderApp(path);

    expect(await screen.findByRole('alert')).toHaveTextContent('Nie udało się wczytać kariery.');
    await user.click(screen.getByRole('button', { name: 'Odśwież' }));

    expect(await screen.findByRole('heading', { name: 'Plan tygodnia' })).toBeInTheDocument();
  });

  it('opens and closes the statistics drawer', async () => {
    rememberTestCareer();
    serveCareer(careerView());
    const { user } = renderApp(path);

    const toggle = await screen.findByRole('button', { name: 'Statystyki' });
    const rail = screen.getByRole('complementary', { name: 'Statystyki' });
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    await user.click(toggle);
    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    expect(rail).toHaveClass('open');
    // On a phone the open drawer would sit below the stage; focus brings the player to it.
    expect(within(rail).getByRole('heading', { name: 'Statystyki' })).toHaveFocus();
    await user.click(toggle);
    expect(rail).not.toHaveClass('open');
  });

  it('shows the ending of a finished career', async () => {
    rememberTestCareer();
    serveCareer(careerView({ status: 'ended', ending: 'completed', next: endedNext() }));

    renderApp(path);

    expect(
      await screen.findByText('Kariera ukończona — dwadzieścia lat streamowania za tobą.'),
    ).toBeInTheDocument();
  });
});
