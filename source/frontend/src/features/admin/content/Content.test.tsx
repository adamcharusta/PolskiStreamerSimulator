import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import type {
  ContentAdminView,
  ContentDraftView,
  ContentPublicationView,
  OwnerSessionView,
  SimulationView,
} from '@/api/generated/game.schemas';

import { problem } from '../../../../test/api';
import { renderApp } from '../../../../test/render';
import { server } from '../../../../test/server';
import { constants, preview, withConstants } from './contentDraft';

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
  text: '{gender, select, female {Zaczęłaś} male {Zacząłeś} other {Zaczęłoś}} debatę.',
  weight: 3,
  choices: [{ id: 'defend', label: 'Broń tezy' }],
};

function draft(overrides: Partial<ContentDraftView> = {}): ContentDraftView {
  return {
    baseNumber: 1,
    revision: 3,
    updatedAt: '2026-10-29T11:00:00Z',
    updatedBy: 'wlasciciel',
    behindCurrent: false,
    balance: { version: 'm3.2', livingCostPln: 100, scoreWeights: { followers: 0.45 } },
    events: [{ id: event.id, version: 1, phase: 'live', title: event.title, event }],
    problems: [],
    diff: { items: [], values: [], mechanical: false, unchanged: true },
    ...overrides,
  };
}

interface Sent {
  readonly method: string;
  readonly path: string;
  readonly body: unknown;
  readonly csrf: string | null;
}

function serve(view: ContentAdminView, publications: ContentPublicationView[] = [release]) {
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
      const body: unknown = await request.json().catch(() => null);
      sent.push({
        method: request.method,
        path: url.pathname + url.search,
        body,
        csrf: request.headers.get('x-csrf-token'),
      });
      if (url.pathname.endsWith('/draft') && request.method === 'POST') {
        current = { ...current, draft: draft() };
        return HttpResponse.json(current.draft);
      }

      if (url.pathname.endsWith('/simulation')) {
        const run = {
          digest: 'd',
          careers: 40,
          endings: { completed: 40 },
          score: { p10: 1, p50: 2, p90: 3 },
          weeks: { p10: 1040, p50: 1040, p90: 1040 },
          violations: [],
          errors: [],
        };
        return HttpResponse.json({
          draft: run,
          current: run,
          passed: true,
        } satisfies SimulationView);
      }

      if (url.pathname.endsWith('/publish')) {
        return HttpResponse.json({ ...release, number: 2, kind: 'publish' });
      }

      return HttpResponse.json(current.draft);
    }),
  );
  return sent;
}

describe('content draft helpers', () => {
  it('lists every balance number by path and sets one back', () => {
    const balance = { a: 1, b: { c: [2, 3] }, d: 'text' };

    expect(constants(balance)).toEqual([
      { path: 'a', value: 1 },
      { path: 'b.c[0]', value: 2 },
      { path: 'b.c[1]', value: 3 },
    ]);
    expect(withConstants(balance, new Map([['b.c[1]', 9]]))).toEqual({
      a: 1,
      b: { c: [2, 9] },
      d: 'text',
    });
    expect(balance.b.c[1]).toBe(3);
  });

  it('previews an event in each pronoun form', () => {
    expect(preview(event, 'woman')).toEqual(['Gorąca teza', 'Zaczęłaś debatę.', 'Broń tezy']);
    expect(preview(event, 'nonbinary')[1]).toBe('Zaczęłoś debatę.');
  });
});

describe('content section', () => {
  it('creates a draft from the current publication', async () => {
    const sent = serve({ current: release, draft: null });
    const { user } = renderApp('/admin');

    const section = await screen.findByRole('region', { name: 'Treść' });
    expect(await within(section).findByText(/publikacji #1 \(wydanie/u)).toBeVisible();
    await user.click(within(section).getByRole('button', { name: 'Utwórz szkic' }));

    expect(await within(section).findByText(/Szkic z publikacji #1, wersja 3/u)).toBeVisible();
    expect(sent[0]).toMatchObject({
      method: 'POST',
      path: '/v1/admin/content/draft',
      csrf: 'csrf-1',
    });
  });

  it('edits an event as JSON with a preview and sends it with the revision', async () => {
    const sent = serve({ current: release, draft: draft() });
    const { user } = renderApp('/admin');

    const events = await screen.findByRole('region', { name: 'Wydarzenia' });
    await user.click(within(events).getByRole('button', { name: /jc-hot-take v1/u }));
    const preview = within(events).getByRole('region', { name: 'Podgląd' });
    expect(preview).toHaveTextContent('Zaczęłaś debatę.');
    expect(preview).toHaveTextContent('Zacząłeś debatę.');

    const editor = within(events).getByLabelText('Wydarzenie jc-hot-take (JSON)');
    await user.clear(editor);
    await user.click(editor);
    await user.paste(JSON.stringify({ ...event, version: 2, title: 'Nowy tytuł' }));
    await user.click(within(events).getByRole('button', { name: 'Zapisz wydarzenie' }));

    await waitFor(() => {
      expect(sent.at(-1)).toMatchObject({
        method: 'PUT',
        path: '/v1/admin/content/draft/events/jc-hot-take',
        body: { revision: 3, event: { title: 'Nowy tytuł', version: 2 } },
        csrf: 'csrf-1',
      });
    });
    await waitFor(() => {
      expect(within(events).getByRole('button', { name: 'Zapisz wydarzenie' })).toBeEnabled();
    });
  });

  it('saves changed balance constants as one document', async () => {
    const sent = serve({ current: release, draft: draft() });
    const { user } = renderApp('/admin');

    const balance = await screen.findByRole('region', { name: 'Stałe balansu' });
    const field = within(balance).getByLabelText('livingCostPln');
    await user.clear(field);
    await user.type(field, '120');
    await user.click(within(balance).getByRole('button', { name: 'Zapisz 1 zmianę' }));

    await waitFor(() => {
      expect(sent.at(-1)).toMatchObject({
        method: 'PUT',
        path: '/v1/admin/content/draft/balance',
        body: { revision: 3, balance: { livingCostPln: 120, scoreWeights: { followers: 0.45 } } },
      });
    });
  });

  it('shows problems and blocks publication', async () => {
    serve({
      current: release,
      draft: draft({
        problems: ['event-version-changed:jc-hot-take@1'],
        diff: {
          items: [
            {
              section: 'event',
              id: 'jc-hot-take',
              change: 'changed',
              mechanical: false,
              versionBefore: 1,
              versionAfter: 1,
            },
          ],
          values: [],
          mechanical: false,
          unchanged: false,
        },
      }),
    });
    renderApp('/admin');

    const problems = await screen.findByRole('list', { name: 'Problemy' });
    expect(problems).toHaveTextContent('event-version-changed:jc-hot-take@1');
    expect(screen.getByText('Tylko teksty: sezon rankingu się nie zmieni.')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Opublikuj' })).toBeDisabled();
  });

  it('simulates and publishes a mechanical change with a reason', async () => {
    const sent = serve({
      current: release,
      draft: draft({
        diff: {
          items: [],
          values: [{ path: 'livingCostPln', before: '100', after: '120', mechanical: true }],
          mechanical: true,
          unchanged: false,
        },
      }),
    });
    const { user } = renderApp('/admin');

    expect(await screen.findByText(/publikacja otworzy nowy sezon/u)).toBeVisible();
    expect(screen.getByText('livingCostPln: 100 → 120')).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'Symuluj 40 karier' }));
    expect(await screen.findByText('Żadna symulowana kariera nie złamała reguł.')).toBeVisible();

    const publish = screen.getByRole('form', { name: 'Publikacja' });
    await user.type(within(publish).getByLabelText('Powód'), 'Droższe życie');
    await user.click(within(publish).getByRole('button', { name: 'Opublikuj' }));

    expect(await screen.findByText(/Opublikowano jako #2/u)).toBeVisible();
    expect(sent.find((s) => s.path.endsWith('/simulation'))?.csrf).toBe('csrf-1');
    expect(sent.find((s) => s.path.endsWith('/publish'))?.body).toEqual({
      revision: 3,
      reason: 'Droższe życie',
    });
  });

  it('reverts an earlier publication with a reason', async () => {
    const sent = serve({ current: { ...release, number: 2, kind: 'publish' }, draft: null }, [
      { ...release, number: 2, kind: 'publish', actor: 'wlasciciel', reason: 'Zmiana' },
      release,
    ]);
    const { user } = renderApp('/admin');

    const history = await screen.findByRole('region', { name: 'Historia publikacji' });
    await user.click(await within(history).findByRole('button', { name: 'Przywróć #1' }));
    expect(within(history).queryByRole('button', { name: 'Przywróć #2' })).not.toBeInTheDocument();
    await user.type(within(history).getByLabelText('Powód'), 'Wracamy');
    await user.click(within(history).getByRole('button', { name: 'Opublikuj ponownie #1' }));

    await waitFor(() => {
      expect(sent.at(-1)).toMatchObject({
        path: '/v1/admin/content/publications/1/revert',
        body: { reason: 'Wracamy' },
        csrf: 'csrf-1',
      });
    });
  });
});
