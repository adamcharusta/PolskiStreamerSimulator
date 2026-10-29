import { fireEvent, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import type { CreateCareerRequest } from '@/api/generated/game.schemas';
import { knownCareers } from '@/careers/knownCareers';
import { pendingCreation } from '@/careers/pendingCreation';

import { problem } from '../../../test/api';
import { careerOptions, careerView } from '../../../test/fixtures';
import { renderApp, TEST_CAREER_ID } from '../../../test/render';
import { server } from '../../../test/server';

const KEY = 'pss1.0199e3a0000070008000000000000001.abcdefghijklmnopqrstuvwxyzABCDEFGHIJ0123-_6';

function serveOptions() {
  server.use(http.get('/v1/career-options', () => HttpResponse.json(careerOptions())));
}

function captureCreation(respond: () => Response) {
  const bodies: CreateCareerRequest[] = [];
  server.use(
    http.post('/v1/careers', async ({ request }) => {
      bodies.push((await request.json()) as CreateCareerRequest);
      return respond();
    }),
  );
  return bodies;
}

describe('character creation', () => {
  it('offers a retry when career options cannot be loaded', async () => {
    server.use(http.get('/v1/career-options', () => problem(500, 'internal-error')));
    const { user } = renderApp('/nowa-kariera');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Nie udało się wczytać opcji kariery.',
    );
    serveOptions();
    await user.click(screen.getByRole('button', { name: 'Spróbuj ponownie' }));

    expect(await screen.findByLabelText('Nick')).toBeInTheDocument();
  });

  it('does not create a career when this browser cannot save its access secret', async () => {
    serveOptions();
    const bodies = captureCreation(() => HttpResponse.json({ careerId: TEST_CAREER_ID }));
    const { user } = renderApp('/nowa-kariera');
    await user.type(await screen.findByLabelText('Nick'), 'Valid Name');

    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('Storage disabled', 'SecurityError');
    });
    try {
      await user.click(screen.getByRole('button', { name: 'Zaczynam karierę' }));
      expect(await screen.findByRole('alert')).toHaveTextContent(
        'Nie można zapisać dostępu do kariery',
      );
      expect(bodies).toEqual([]);
    } finally {
      setItem.mockRestore();
    }
  });

  it('repeats a creation after a lost connection with the same creation ID and keeps it pending', async () => {
    serveOptions();
    const bodies = captureCreation(() => HttpResponse.error());
    const { user } = renderApp('/nowa-kariera');

    await user.type(await screen.findByLabelText('Nick'), 'Valid Name');
    await user.click(screen.getByRole('button', { name: 'Zaczynam karierę' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Nie udało się założyć kariery.');
    expect(bodies).toHaveLength(3);
    expect(new Set(bodies.map((body) => body.creationId)).size).toBe(1);
    expect(pendingCreation()?.creationId).toBe(bodies[0]?.creationId);
  });

  it('shows the recovery key when the browser cannot store a created career', async () => {
    serveOptions();
    captureCreation(() =>
      HttpResponse.json(
        {
          careerId: TEST_CAREER_ID,
          secret: 'fresh-secret',
          recoveryKey: KEY,
          career: careerView(),
        },
        { status: 201 },
      ),
    );
    const { user } = renderApp('/nowa-kariera');
    await user.type(await screen.findByLabelText('Nick'), 'Valid Name');
    // Only the career list is refused: the pending creation is written before the request.
    const setItem = Storage.prototype.setItem.bind(localStorage);
    const spy = vi.spyOn(Storage.prototype, 'setItem').mockImplementation((key, value) => {
      if (key === 'pss.careers.v1') {
        throw new DOMException('Quota exceeded', 'QuotaExceededError');
      }
      setItem(key, value);
    });
    try {
      await user.click(screen.getByRole('button', { name: 'Zaczynam karierę' }));

      expect(await screen.findByText(/nie zapisała dostępu/u)).toBeInTheDocument();
      expect(screen.getByRole('status', { name: 'Klucz odzyskiwania' })).toHaveTextContent(KEY);
      expect(screen.getByRole('link', { name: 'Odzyskaj karierę kluczem' })).toBeInTheDocument();
      expect(pendingCreation()).toBeNull();
    } finally {
      spy.mockRestore();
    }
  });

  it('shows each background with its starting numbers from the API', async () => {
    serveOptions();
    renderApp('/nowa-kariera');

    const dorm = await screen.findByRole('radio', { name: /Świeżak z akademika/u });
    expect(dorm).toBeChecked();
    expect(dorm.closest('label')).toHaveTextContent(/Kasa: 2000\s+zł/u);
    expect(dorm.closest('label')).toHaveTextContent('Mocna strona: FPS');
    expect(
      screen.getByRole('radio', { name: /Człowiek z branży/u }).closest('label'),
    ).toHaveTextContent('×0,90');
  });

  it('names no strong side for a background with even skills', async () => {
    serveOptions();
    renderApp('/nowa-kariera');

    const insider = await screen.findByRole('radio', { name: /Człowiek z branży/u });

    expect(insider.closest('label')).toHaveTextContent(
      'Mocna strona: brak — żadna kategoria się nie wyróżnia',
    );
  });

  it('creates one career when the start button is activated twice at once', async () => {
    serveOptions();
    const bodies = captureCreation(() =>
      HttpResponse.json(
        {
          careerId: TEST_CAREER_ID,
          secret: 'fresh-secret',
          recoveryKey: KEY,
          career: careerView(),
        },
        { status: 201 },
      ),
    );
    const { user, router } = renderApp('/nowa-kariera');
    await user.type(await screen.findByLabelText('Nick'), 'Valid Name');

    const submit = screen.getByRole('button', { name: 'Zaczynam karierę' });
    fireEvent.click(submit);
    fireEvent.click(submit);

    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/kariera/${TEST_CAREER_ID}/klucz`);
    });
    expect(bodies).toHaveLength(1);
  });

  it('shows the generic error when the server rejects a field the form does not show', async () => {
    serveOptions();
    captureCreation(() =>
      problem(400, 'validation-failed', { errors: { background: ['Unknown background.'] } }),
    );
    const { user } = renderApp('/nowa-kariera');

    await user.type(await screen.findByLabelText('Nick'), 'Valid Name');
    await user.click(screen.getByRole('button', { name: 'Zaczynam karierę' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Nie udało się założyć kariery. Spróbuj ponownie.',
    );
  });

  it('rejects an invalid nickname before sending anything', async () => {
    serveOptions();
    const bodies = captureCreation(() => HttpResponse.json({}));
    const { user } = renderApp('/nowa-kariera');

    await user.type(await screen.findByLabelText('Nick'), 'ab');
    await user.click(screen.getByRole('button', { name: 'Zaczynam karierę' }));

    expect(
      await screen.findByText('Nick musi mieć 3–20 znaków: litery, cyfry, spacja, _ - .'),
    ).toBeInTheDocument();
    expect(screen.getByLabelText('Nick')).toHaveAttribute('aria-invalid', 'true');
    expect(bodies).toEqual([]);
  });

  it('sends the chosen profile, remembers the career and opens its recovery key', async () => {
    serveOptions();
    const bodies = captureCreation(() =>
      HttpResponse.json(
        {
          careerId: TEST_CAREER_ID,
          secret: 'fresh-secret',
          recoveryKey: KEY,
          career: careerView({ nickname: 'Zażółć 1' }),
        },
        { status: 201 },
      ),
    );
    const { user, router } = renderApp('/nowa-kariera');

    await user.type(await screen.findByLabelText('Nick'), '  Zażółć 1 ');
    await user.click(screen.getByRole('radio', { name: /Lokalny memiarz/u }));
    await user.click(screen.getByRole('radio', { name: /Łatwy/u }));
    await user.click(screen.getByRole('radio', { name: 'forma neutralna' }));
    expect(screen.getByRole('region', { name: 'Podsumowanie' })).toHaveTextContent(
      'Lokalny memiarz',
    );
    await user.click(screen.getByRole('button', { name: 'Zaczynam karierę' }));

    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/kariera/${TEST_CAREER_ID}/klucz`);
    });
    expect(bodies).toEqual([
      {
        nickname: 'Zażółć 1',
        background: 'localMemeCreator',
        difficulty: 'easy',
        pronouns: 'nonbinary',
        creationId: expect.stringMatching(/^[0-9a-f-]{36}$/u) as string,
      },
    ]);
    expect(knownCareers()[0]).toMatchObject({
      careerId: TEST_CAREER_ID,
      nickname: 'Zażółć 1',
      secret: 'fresh-secret',
      recoveryKey: KEY,
    });
    expect(pendingCreation()).toBeNull();
    expect(await screen.findByRole('status', { name: 'Klucz odzyskiwania' })).toHaveTextContent(
      KEY,
    );
  });

  it('states the retention rule and the recovery key before the career starts', async () => {
    serveOptions();
    renderApp('/nowa-kariera');

    expect(await screen.findByText(/dostaniesz klucz odzyskiwania/u)).toHaveTextContent(
      '6 miesiącach',
    );
  });

  it('shows the server field error on the nickname', async () => {
    serveOptions();
    captureCreation(() =>
      problem(400, 'validation-failed', { errors: { nickname: ['Nickname is invalid.'] } }),
    );
    const { user } = renderApp('/nowa-kariera');

    await user.type(await screen.findByLabelText('Nick'), 'Valid Name');
    await user.click(screen.getByRole('button', { name: 'Zaczynam karierę' }));

    expect(
      await screen.findByText('Nick musi mieć 3–20 znaków: litery, cyfry, spacja, _ - .'),
    ).toBeInTheDocument();
  });

  it('shows a generic error when the server fails', async () => {
    serveOptions();
    captureCreation(() => problem(500, 'internal-error'));
    const { user } = renderApp('/nowa-kariera');

    await user.type(await screen.findByLabelText('Nick'), 'Valid Name');
    await user.click(screen.getByRole('button', { name: 'Zaczynam karierę' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Nie udało się założyć kariery. Spróbuj ponownie.',
    );
  });
});
