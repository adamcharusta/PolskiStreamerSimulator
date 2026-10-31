import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import type {
  ContentAdminView,
  ContentDraftView,
  ContentPublicationView,
  OwnerSessionView,
} from '@/api/generated/game.schemas';

import { problem } from '../../../../test/api';
import { renderApp } from '../../../../test/render';
import { server } from '../../../../test/server';

const session: OwnerSessionView = {
  username: 'wlasciciel',
  csrfToken: 'csrf-1',
  recoveryCodesLeft: 10,
};

const release: ContentPublicationView = {
  number: 1,
  digest: 'a'.repeat(64),
  mechanicsHash: 'b'.repeat(64),
  kind: 'release',
  sourceNumber: null,
  actor: 'deployment',
  reason: 'repository content',
  publishedAt: '2026-10-29T10:00:00Z',
};

const event = {
  id: 'jc-hot-take',
  version: 1,
  phase: 'live',
  title: 'Gorąca teza',
  text: 'Tekst.',
  choices: [],
};

function draft(overrides: Partial<ContentDraftView> = {}): ContentDraftView {
  return {
    baseNumber: 1,
    revision: 3,
    updatedAt: '2026-10-29T11:00:00Z',
    updatedBy: 'wlasciciel',
    behindCurrent: false,
    balance: { livingCostPln: 100, scoreWeights: { followers: 0.45 } },
    events: [{ id: event.id, version: 1, phase: 'live', title: event.title, event }],
    problems: [],
    diff: { items: [], values: [], mechanical: false, unchanged: true },
    ...overrides,
  };
}

interface Sent {
  readonly method: string;
  readonly path: string;
  readonly csrf: string | null;
}

/** The panel's endpoints; the draft endpoints answer with <paramref>respond</paramref> or the draft. */
function serve(
  view: ContentAdminView,
  respond: (path: string, body: string) => Response | null = () => null,
  publications: ContentPublicationView[] = [release],
) {
  const sent: Sent[] = [];
  let current = view;
  server.use(
    http.get('/v1/admin/session', () => HttpResponse.json(session)),
    http.get('/v1/seasons', () => HttpResponse.json([])),
    http.get('/v1/admin/leaderboard', () => problem(404, 'season-not-found')),
    http.get('/v1/admin/audit', () => HttpResponse.json([])),
    http.get('/v1/admin/content', () => HttpResponse.json(current)),
    http.get('/v1/admin/content/publications', () => HttpResponse.json(publications)),
    http.all('/v1/admin/content/*', async ({ request }) => {
      const url = new URL(request.url);
      const text =
        request.method === 'GET' || request.method === 'DELETE' ? '' : await request.text();
      sent.push({
        method: request.method,
        path: url.pathname + url.search,
        csrf: request.headers.get('x-csrf-token'),
      });
      const custom = respond(url.pathname, text);
      if (custom !== null) {
        return custom;
      }

      if (url.pathname.endsWith('/draft') && request.method === 'DELETE') {
        current = { ...current, draft: null };
        return new HttpResponse(null, { status: 204 });
      }

      return HttpResponse.json(current.draft);
    }),
  );
  return sent;
}

describe('content editor edge cases', () => {
  it('explains bad JSON, a missing id and a stale draft, then removes an event', async () => {
    const sent = serve({ current: release, draft: draft({ behindCurrent: true }) }, (path, body) =>
      path.includes('/draft/events/') && body.includes('"stale"')
        ? problem(409, 'draft-stale')
        : null,
    );
    const { user } = renderApp('/admin');

    expect(await screen.findByText(/opublikowano nowszą treść/u)).toBeVisible();
    const events = screen.getByRole('region', { name: 'Wydarzenia' });
    await user.type(within(events).getByLabelText('Szukaj wydarzenia'), 'brak');
    expect(
      within(events).queryByRole('button', { name: /jc-hot-take v1/u }),
    ).not.toBeInTheDocument();
    await user.clear(within(events).getByLabelText('Szukaj wydarzenia'));
    await user.click(within(events).getByRole('button', { name: /jc-hot-take v1/u }));
    const editor = within(events).getByLabelText('Wydarzenie jc-hot-take (JSON)');

    await user.clear(editor);
    await user.type(editor, 'nie json');
    await user.click(within(events).getByRole('button', { name: 'Zapisz wydarzenie' }));
    expect(within(events).getByRole('alert')).toHaveTextContent('To nie jest poprawny JSON.');

    await user.clear(editor);
    await user.click(editor);
    await user.paste('{"title":"bez id"}');
    await user.click(within(events).getByRole('button', { name: 'Zapisz wydarzenie' }));
    expect(within(events).getByRole('alert')).toHaveTextContent('Wydarzenie musi mieć pole id.');

    await user.clear(editor);
    await user.click(editor);
    await user.paste(JSON.stringify({ ...event, title: 'stale' }));
    await user.click(within(events).getByRole('button', { name: 'Zapisz wydarzenie' }));
    expect(await within(events).findByText(/w innej karcie/u)).toBeVisible();

    await user.click(within(events).getByRole('button', { name: 'Usuń ze szkicu' }));
    await waitFor(() => {
      expect(sent.at(-1)).toMatchObject({
        method: 'DELETE',
        path: '/v1/admin/content/draft/events/jc-hot-take?revision=3',
      });
    });
  });

  it('starts a new event from a template', async () => {
    serve({ current: release, draft: draft() });
    const { user } = renderApp('/admin');

    const events = await screen.findByRole('region', { name: 'Wydarzenia' });
    await user.click(within(events).getByRole('button', { name: 'Nowe wydarzenie' }));

    const editor = within(events).getByLabelText<HTMLTextAreaElement>('Wydarzenie nowe-2 (JSON)');
    expect(JSON.parse(editor.value)).toMatchObject({ id: 'nowe-2', version: 1 });
    expect(within(events).getByRole('region', { name: 'Podgląd' })).toHaveTextContent(
      'Nowe wydarzenie',
    );
    expect(
      within(events).queryByRole('button', { name: 'Usuń ze szkicu' }),
    ).not.toBeInTheDocument();
  });

  it('refuses a constant that is not a number and explains a stale save', async () => {
    const sent = serve({ current: release, draft: draft() }, (path) =>
      path.endsWith('/balance') ? problem(409, 'draft-stale') : null,
    );
    const { user } = renderApp('/admin');

    const balance = await screen.findByRole('region', { name: 'Stałe balansu' });
    await user.type(within(balance).getByLabelText('Szukaj stałej'), 'followers');
    expect(within(balance).queryByLabelText('livingCostPln')).not.toBeInTheDocument();
    const field = within(balance).getByLabelText('scoreWeights.followers');
    await user.clear(field);
    await user.type(field, 'dużo');
    await user.click(within(balance).getByRole('button', { name: 'Zapisz 1 zmianę' }));
    expect(within(balance).getByRole('alert')).toHaveTextContent('Każda stała musi być liczbą.');
    expect(sent.filter((s) => s.method === 'PUT')).toEqual([]);

    await user.clear(field);
    await user.type(field, '0,5');
    await user.click(within(balance).getByRole('button', { name: 'Zapisz 1 zmianę' }));
    expect(await within(balance).findByText(/w innej karcie/u)).toBeVisible();
  });

  it('refuses to publish without a reason and discards the draft', async () => {
    const sent = serve({
      current: release,
      draft: draft({
        diff: {
          items: [],
          values: [{ path: 'version', before: '"a"', after: '"b"', mechanical: false }],
          mechanical: false,
          unchanged: false,
        },
      }),
    });
    const { user } = renderApp('/admin');

    const publish = await screen.findByRole('form', { name: 'Publikacja' });
    await user.click(within(publish).getByRole('button', { name: 'Opublikuj' }));
    expect(await screen.findByText(/Podaj powód/u)).toBeVisible();
    expect(sent.filter((s) => s.path.endsWith('/publish'))).toEqual([]);

    await user.click(screen.getByRole('button', { name: 'Odrzuć szkic' }));
    expect(await screen.findByRole('button', { name: 'Utwórz szkic' })).toBeVisible();
    expect(sent.at(-1)).toMatchObject({
      method: 'DELETE',
      path: '/v1/admin/content/draft?revision=3',
      csrf: 'csrf-1',
    });
  });

  it('shows a failed and a broken simulation', async () => {
    let calls = 0;
    serve({ current: release, draft: draft() }, (path) => {
      if (!path.endsWith('/simulation')) {
        return null;
      }

      calls += 1;
      const run = {
        digest: 'd',
        careers: 40,
        endings: { completed: 39 },
        score: { p10: 1, p50: 2, p90: 3 },
        weeks: { p10: 1, p50: 2, p90: 3 },
        violations: ['career 3: cash'],
        errors: [],
      };
      return calls === 1
        ? problem(422, 'content-invalid')
        : HttpResponse.json({ draft: run, current: run, passed: false });
    });
    const { user } = renderApp('/admin');

    const simulation = await screen.findByRole('region', { name: 'Symulacja' });
    await user.click(within(simulation).getByRole('button', { name: 'Symuluj 40 karier' }));
    expect(await within(simulation).findByRole('alert')).toHaveTextContent(
      'Symulacja się nie udała.',
    );
    await user.click(within(simulation).getByRole('button', { name: 'Symuluj 40 karier' }));
    expect(await within(simulation).findByText(/Symulacja wykryła błędy/u)).toBeVisible();
    expect(within(simulation).getByText('career 3: cash')).toBeVisible();
  });

  it('exports a publication and explains a revert to the current content', async () => {
    serve(
      { current: { ...release, number: 2, kind: 'publish' }, draft: null },
      (path) => {
        if (path.endsWith('/export')) {
          return HttpResponse.json({
            number: 1,
            digest: release.digest,
            mechanicsHash: release.mechanicsHash,
            balanceJson: '{}',
            eventsJson: '{}',
          });
        }

        return path.endsWith('/revert') ? problem(409, 'content-unchanged') : null;
      },
      [{ ...release, number: 2, kind: 'publish', actor: 'wlasciciel', reason: 'Zmiana' }, release],
    );
    const blobs: Blob[] = [];
    Object.assign(URL, {
      createObjectURL: vi.fn((blob: Blob) => {
        blobs.push(blob);
        return 'blob:content';
      }),
      revokeObjectURL: vi.fn(),
    });
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => null);
    const { user } = renderApp('/admin');

    const history = await screen.findByRole('region', { name: 'Historia publikacji' });
    await user.click(await within(history).findByRole('button', { name: 'Eksportuj #1' }));
    await waitFor(() => {
      expect(click).toHaveBeenCalledOnce();
    });
    expect((click.mock.contexts[0] as HTMLAnchorElement).download).toBe(
      `tresc-1-${release.digest.slice(0, 12)}.json`,
    );
    expect(JSON.parse((await blobs[0]?.text()) ?? '')).toMatchObject({ digest: release.digest });

    await user.click(within(history).getByRole('button', { name: 'Przywróć #1' }));
    await user.click(within(history).getByRole('button', { name: 'Opublikuj ponownie #1' }));
    expect(within(history).getByRole('alert')).toHaveTextContent(/Podaj powód/u);
    await user.type(within(history).getByLabelText('Powód'), 'Jeszcze raz');
    await user.click(within(history).getByRole('button', { name: 'Opublikuj ponownie #1' }));
    expect(await within(history).findByText('Ta treść jest już obecna.')).toBeVisible();
    await user.click(within(history).getByRole('button', { name: 'Anuluj' }));
    expect(within(history).getByRole('button', { name: 'Przywróć #1' })).toBeVisible();
    click.mockRestore();
  });
});
