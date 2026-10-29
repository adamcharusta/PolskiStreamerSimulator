import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import type {
  AuditRecordView,
  ModeratedEntryView,
  ModeratedSeasonView,
  OwnerSessionView,
  SeasonView,
} from '@/api/generated/game.schemas';

import { problem } from '../../../test/api';
import { renderApp } from '../../../test/render';
import { server } from '../../../test/server';

const session: OwnerSessionView = {
  username: 'wlasciciel',
  csrfToken: 'csrf-1',
  recoveryCodesLeft: 10,
};

const open: SeasonView = {
  number: 2,
  openedAt: '2026-10-01T00:00:00Z',
  closedAt: null,
  submissionDeadline: null,
  extended: false,
  current: true,
};

const closed: SeasonView = {
  number: 1,
  openedAt: '2026-09-01T00:00:00Z',
  closedAt: '2026-10-01T00:00:00Z',
  submissionDeadline: '2026-12-30T00:00:00Z',
  extended: false,
  current: false,
};

function entry(overrides: Partial<ModeratedEntryView> = {}): ModeratedEntryView {
  return {
    entryId: '0199e3a0-0000-7000-8000-0000000000e1',
    careerId: '0199e3a0-0000-7000-8000-0000000000c1',
    rank: 1,
    nickname: 'ZLY',
    score: 45_000,
    weeksPlayed: 12,
    difficulty: 'normal',
    ending: 'earlyRetirement',
    submittedAt: '2026-10-10T10:00:00Z',
    rulesetId: 'm3-systems.2',
    finalStateVersion: 40,
    finalStateHash: 'a'.repeat(64),
    hiddenAt: null,
    hiddenReason: null,
    ...overrides,
  };
}

interface Posted {
  readonly path: string;
  readonly body: unknown;
  readonly csrf: string | null;
}

function servePanel(entries: ModeratedEntryView[], audit: AuditRecordView[] = []) {
  const posted: Posted[] = [];
  let current = entries;
  const board = (): ModeratedSeasonView => ({ season: open, entries: current });
  server.use(
    http.get('/v1/admin/session', () => HttpResponse.json(session)),
    http.get('/v1/seasons', () => HttpResponse.json([open, closed])),
    http.get('/v1/admin/leaderboard', () => HttpResponse.json(board())),
    http.get('/v1/admin/audit', () => HttpResponse.json(audit)),
    http.post('/v1/admin/*', async ({ request }) => {
      const path = new URL(request.url).pathname;
      const body: unknown = await request.json().catch(() => null);
      posted.push({ path, body, csrf: request.headers.get('x-csrf-token') });
      if (path.endsWith('/hide')) {
        current = current.map((e) => ({
          ...e,
          rank: null,
          hiddenAt: '2026-11-01T00:00:00Z',
          hiddenReason: 'Spam',
        }));
        return HttpResponse.json(current[0]);
      }

      if (path.endsWith('/extension')) {
        return HttpResponse.json({
          ...closed,
          submissionDeadline: '2027-02-01T00:00:00Z',
          extended: true,
        });
      }

      return new HttpResponse(null, { status: 204 });
    }),
  );
  return posted;
}

describe('admin sign-in', () => {
  it('signs in with the password and then the authenticator code', async () => {
    let signedIn = false;
    const sent: unknown[] = [];
    server.use(
      http.get('/v1/admin/session', () =>
        signedIn ? HttpResponse.json(session) : problem(401, 'owner-unauthorized'),
      ),
      http.post('/v1/admin/login', async ({ request }) => {
        sent.push(await request.json());
        return HttpResponse.json({ next: 'code' });
      }),
      http.post('/v1/admin/login/code', async ({ request }) => {
        sent.push(await request.json());
        signedIn = true;
        return HttpResponse.json(session);
      }),
      http.get('/v1/seasons', () => HttpResponse.json([open])),
      http.get('/v1/admin/leaderboard', () => HttpResponse.json({ season: open, entries: [] })),
      http.get('/v1/admin/audit', () => HttpResponse.json([])),
    );
    const { user } = renderApp('/admin');

    await user.type(await screen.findByLabelText('Nazwa użytkownika'), 'wlasciciel');
    await user.type(screen.getByLabelText('Hasło'), 'tajne-haslo');
    await user.click(screen.getByRole('button', { name: 'Dalej' }));
    await user.type(await screen.findByLabelText('Kod z aplikacji'), '123 456');
    await user.click(screen.getByRole('button', { name: 'Zaloguj' }));

    expect(await screen.findByText('Zalogowano jako wlasciciel.')).toBeVisible();
    expect(sent).toEqual([{ username: 'wlasciciel', password: 'tajne-haslo' }, { code: '123456' }]);
  });

  it('sends a recovery code and explains a failed or locked sign-in', async () => {
    const codes: unknown[] = [];
    server.use(
      http.get('/v1/admin/session', () => problem(401, 'owner-unauthorized')),
      http.post('/v1/admin/login', () => HttpResponse.json({ next: 'code' })),
      http.post('/v1/admin/login/code', async ({ request }) => {
        codes.push(await request.json());
        return codes.length === 1
          ? problem(401, 'owner-login-failed')
          : problem(423, 'owner-locked-out');
      }),
    );
    const { user } = renderApp('/admin');

    await user.type(await screen.findByLabelText('Nazwa użytkownika'), 'w');
    await user.type(screen.getByLabelText('Hasło'), 'p');
    await user.click(screen.getByRole('button', { name: 'Dalej' }));
    await user.click(await screen.findByRole('button', { name: 'Użyj kodu zapasowego' }));
    await user.type(screen.getByLabelText('Kod zapasowy'), ' ABCDE-12345 ');
    await user.click(screen.getByRole('button', { name: 'Zaloguj' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Nieprawidłowe dane logowania.');
    await user.type(screen.getByLabelText('Kod zapasowy'), 'X');
    await user.click(screen.getByRole('button', { name: 'Zaloguj' }));
    expect(await screen.findByText(/zablokowane na 15 minut/u)).toBeVisible();
    expect(codes[0]).toEqual({ recoveryCode: 'ABCDE-12345' });
  });
});

describe('admin panel', () => {
  it('hides an entry with a reason and the anti-forgery header', async () => {
    const posted = servePanel([entry()]);
    const { user } = renderApp('/admin');

    const list = await screen.findByRole('list', { name: 'Wpisy sezonu' });
    expect(within(list).getByText(/Miejsce 1\./u)).toBeVisible();
    await user.click(within(list).getByRole('button', { name: 'Ukryj ZLY' }));
    await user.click(screen.getByRole('button', { name: 'Ukryj wpis ZLY' }));
    expect(await screen.findByText(/Podaj powód/u)).toBeVisible();
    expect(posted).toEqual([]);

    await user.type(within(list).getByLabelText('Powód'), 'Spam');
    await user.click(screen.getByRole('button', { name: 'Ukryj wpis ZLY' }));

    expect(await within(list).findByText(/Ukryty: Spam/u)).toBeVisible();
    expect(posted).toEqual([
      {
        path: '/v1/admin/leaderboard/0199e3a0-0000-7000-8000-0000000000e1/hide',
        body: { reason: 'Spam' },
        csrf: 'csrf-1',
      },
    ]);
    expect(within(list).getByRole('button', { name: 'Przywróć ZLY' })).toBeVisible();
  });

  it('extends a closed season and lists the audit trail', async () => {
    const posted = servePanel(
      [],
      [
        {
          id: 1,
          at: '2026-11-01T10:00:00Z',
          actor: 'wlasciciel',
          action: 'leaderboard.hide',
          target: 'e1',
          reason: 'Spam',
          oldJson: null,
          newJson: null,
        },
      ],
    );
    const { user } = renderApp('/admin');

    const audit = await screen.findByRole('region', { name: 'Dziennik zmian' });
    expect(await within(audit).findByText('Ukrycie wpisu')).toBeVisible();
    expect(audit).toHaveTextContent(/e1 — wlasciciel, .*: Spam/u);

    const extension = screen.getByRole('region', { name: 'Termin sezonu' });
    await user.type(within(extension).getByLabelText('Nowy termin'), '2027-02-01T12:00');
    await user.type(within(extension).getByLabelText('Powód'), 'Awaria');
    await user.click(within(extension).getByRole('button', { name: 'Przedłuż termin' }));

    expect(await within(extension).findByText(/Sezon 1 przyjmuje wyniki do/u)).toBeVisible();
    expect(posted[0]?.path).toBe('/v1/admin/seasons/1/extension');
    expect(posted[0]?.body).toMatchObject({ reason: 'Awaria' });
    expect(posted[0]?.csrf).toBe('csrf-1');
  });

  it('warns about few recovery codes and signs out', async () => {
    servePanel([]);
    let signedIn = true;
    server.use(
      http.get('/v1/admin/session', () =>
        signedIn
          ? HttpResponse.json({ ...session, recoveryCodesLeft: 2 })
          : problem(401, 'owner-unauthorized'),
      ),
      http.post('/v1/admin/logout', ({ request }) => {
        signedIn = request.headers.get('x-csrf-token') !== 'csrf-1';
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const { user } = renderApp('/admin');

    expect(await screen.findByText(/Zostały Ci 2 kody zapasowe/u)).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'Wyloguj' }));

    await waitFor(() => {
      expect(screen.getByLabelText('Nazwa użytkownika')).toBeVisible();
    });
  });
});
