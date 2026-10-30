import { act, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import type { SeasonView, TelemetryRequest } from '@/api/generated/game.schemas';

import { problem } from '../../test/api';
import { renderApp } from '../../test/render';
import { server } from '../../test/server';
import {
  allowed,
  buffered,
  flush,
  resetAnalyticsForTests,
  track,
  useConsent,
  weekStage,
} from './analytics';

function captureTelemetry(status = 202) {
  const received: TelemetryRequest[] = [];
  const cookies: (string | null)[] = [];
  server.use(
    http.post('/v1/telemetry', async ({ request }) => {
      received.push((await request.json()) as TelemetryRequest);
      cookies.push(request.headers.get('cookie'));
      return status === 202
        ? HttpResponse.json({ accepted: 1 }, { status })
        : problem(status, 'internal-error');
    }),
  );
  return { received, cookies };
}

beforeEach(() => {
  resetAnalyticsForTests();
});

afterEach(() => {
  resetAnalyticsForTests();
});

describe('analytics module', () => {
  it('collects and sends nothing before consent or after a refusal', async () => {
    const { received } = captureTelemetry();

    track('save_imported');
    await flush();
    useConsent.getState().setConsent('denied');
    track('save_imported');
    await flush();

    expect(buffered()).toEqual([]);
    expect(received).toEqual([]);
  });

  it('buffers allowlisted events after consent and removes them once accepted', async () => {
    const { received, cookies } = captureTelemetry();
    useConsent.getState().setConsent('granted');

    track('career_created', { background: 'dormRoomNewcomer', difficulty: 'easy', ranked: true });
    track('career_created', { nickname: 'Ala' });
    track('batch_run', { weeks: 7 });
    expect(buffered()).toHaveLength(1);
    await flush();

    expect(received).toEqual([
      {
        events: [
          {
            name: 'career_created',
            props: { background: 'dormRoomNewcomer', difficulty: 'easy', ranked: true },
          },
        ],
      },
    ]);
    expect(cookies).toEqual([null]);
    expect(buffered()).toEqual([]);
    expect(localStorage.getItem('pss.analytics.v1')).toBe('granted');
  });

  it('keeps events after a failed send and deletes them on revocation', async () => {
    const { received } = captureTelemetry(500);
    useConsent.getState().setConsent('granted');
    track('save_restored');

    await flush();
    expect(received).toHaveLength(1);
    expect(buffered()).toHaveLength(1);
    expect(localStorage.getItem('pss.analytics.buffer.v1')).not.toBeNull();

    useConsent.getState().setConsent('denied');
    expect(buffered()).toEqual([]);
    expect(localStorage.getItem('pss.analytics.buffer.v1')).toBeNull();
  });

  it('keeps the newest hundred events and sends fifty at a time', async () => {
    const { received } = captureTelemetry();
    useConsent.getState().setConsent('granted');
    for (let i = 0; i < 120; i += 1) {
      track('save_imported');
    }

    expect(buffered()).toHaveLength(100);
    await flush();
    expect(received[0]?.events).toHaveLength(50);
    expect(buffered()).toHaveLength(50);
  });

  it.each([
    [1, '1-4'],
    [13, '5-13'],
    [52, '14-52'],
    [260, '53-260'],
    [1040, '261+'],
  ])('puts week %d in stage %s', (week, stage) => {
    expect(weekStage(week)).toBe(stage);
  });

  it('rejects free text even for enum props', () => {
    expect(allowed('career_ended', { ending: 'earlyRetirement', ranked: false })).toBe(true);
    expect(allowed('career_ended', { ending: 'Jan Kowalski <b>' })).toBe(false);
    expect(allowed('unknown', {})).toBe(false);
  });
});

describe('consent panel and settings', () => {
  it('asks once after the gate and stays away after an answer', async () => {
    const { user } = renderApp('/');

    const panel = await screen.findByRole('region', { name: 'Pomóż ulepszyć grę?' });
    expect(panel).toHaveTextContent(/Bez nicku, kariery, klucza i zapisu/u);
    await user.click(screen.getByRole('button', { name: 'Nie, dziękuję' }));

    expect(screen.queryByRole('region', { name: 'Pomóż ulepszyć grę?' })).not.toBeInTheDocument();
    expect(localStorage.getItem('pss.analytics.v1')).toBe('denied');
  });

  it('switches consent on and off in the settings', async () => {
    const { user } = renderApp('/');
    await user.click(await screen.findByRole('button', { name: 'Zgadzam się' }));
    track('save_imported');

    await user.click(screen.getByRole('button', { name: 'Ustawienia' }));
    const toggle = screen.getByRole('checkbox', { name: 'Wysyłaj anonimowe liczniki rozgrywki' });
    expect(toggle).toBeChecked();
    await user.click(toggle);

    expect(toggle).not.toBeChecked();
    expect(buffered()).toEqual([]);
    expect(useConsent.getState().consent).toBe('denied');
  });
});

describe('notices', () => {
  it('says that play is paused while the server is unreachable and clears after a response', async () => {
    let up = false;
    server.use(
      http.get('/v1/leaderboard', () =>
        up ? HttpResponse.json({ season: null, entries: [] }) : HttpResponse.error(),
      ),
      http.get('/health', () => (up ? HttpResponse.text('ok') : HttpResponse.error())),
    );
    renderApp('/ranking');

    expect(
      await screen.findByText(/Gra jest wstrzymana/u, undefined, { timeout: 5000 }),
    ).toBeVisible();
    up = true;
    act(() => {
      globalThis.dispatchEvent(new Event('online'));
    });
    await waitFor(
      () => {
        expect(screen.queryByText(/Gra jest wstrzymana/u)).not.toBeInTheDocument();
      },
      { timeout: 20_000 },
    );
  }, 30_000);

  it('states the day-of-progress risk and announces extended deadlines', async () => {
    const extended: SeasonView = {
      number: 1,
      openedAt: '2026-09-01T00:00:00Z',
      closedAt: '2026-10-01T00:00:00Z',
      submissionDeadline: '2099-01-30T12:00:00Z',
      extended: true,
      current: false,
    };
    server.use(
      http.get('/v1/seasons', () =>
        HttpResponse.json([extended, { ...extended, number: 2, extended: false }]),
      ),
    );
    renderApp('/');

    const list = await screen.findByRole('list', { name: 'Przedłużone terminy' });
    expect(list).toHaveTextContent(/z sezonu 1 przedłużono do 30 sty 2099/u);
    expect(list).not.toHaveTextContent(/sezonu 2/u);
    expect(screen.getByText(/stracić do około jednego dnia postępu/u)).toBeVisible();
  });
});
