import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { getGetCareerQueryKey } from '@/api/generated/game';
import type {
  CreatedCareer,
  RestoreSlotRequest,
  SaveSlotsView,
  SaveSlotView,
  SaveToSlotRequest,
} from '@/api/generated/game.schemas';
import { knownCareer } from '@/careers/knownCareers';

import { problem, serveCareer } from '../../../test/api';
import { actionOutcome, careerView } from '../../../test/fixtures';
import { rememberTestCareer, renderApp, TEST_CAREER_ID } from '../../../test/render';
import { server } from '../../../test/server';
import { parseSaveFile, saveFileName } from './saveFile';

const empty = (slot: number): SaveSlotView => ({
  slot,
  empty: true,
  savedAt: null,
  week: null,
  lastSettledWeek: null,
  status: null,
  cashPln: null,
  followers: null,
});

const filled = (slot: number, week: number): SaveSlotView => ({
  slot,
  empty: false,
  savedAt: '2026-11-02T10:00:00Z',
  week,
  lastSettledWeek: week - 1,
  status: 'active',
  cashPln: 1500,
  followers: 42,
});

function serveSlots(...views: SaveSlotsView[]) {
  let call = 0;
  server.use(
    http.get('/v1/careers/:id/saves', () => {
      const view = views[Math.min(call, views.length - 1)];
      call += 1;
      return HttpResponse.json(view);
    }),
  );
}

function captureSaves(respond: (slot: number) => Response) {
  const received: { slot: number; body: SaveToSlotRequest }[] = [];
  server.use(
    http.put('/v1/careers/:id/saves/:slot', async ({ request, params }) => {
      const slot = Number(params.slot);
      received.push({ slot, body: (await request.json()) as SaveToSlotRequest });
      return respond(slot);
    }),
  );
  return received;
}

function captureRestores(respond: (slot: number) => Response) {
  const received: { slot: number; body: RestoreSlotRequest; authorization: string | null }[] = [];
  server.use(
    http.post('/v1/careers/:id/saves/:slot/restore', async ({ request, params }) => {
      const slot = Number(params.slot);
      received.push({
        slot,
        body: (await request.json()) as RestoreSlotRequest,
        authorization: request.headers.get('authorization'),
      });
      return respond(slot);
    }),
  );
  return received;
}

const current = careerView({ version: 9, week: 5, lastSettledWeek: 4 });

describe('save file helpers', () => {
  it.each([
    ['Żaba Łoś', 3, 'kariera-zaba-los-tydzien-3.json'],
    ['__', 1, 'kariera-zapis-tydzien-1.json'],
  ])('names the file of %j in week %d %j', (nickname, week, name) => {
    expect(saveFileName(nickname, week)).toBe(name);
  });

  it('reads JSON and nothing else', () => {
    expect(parseSaveFile('{"a":1}')).toEqual({ a: 1 });
    expect(parseSaveFile('not json')).toBeNull();
  });
});

describe('saves screen', () => {
  it('saves into an empty slot at once with the shown version', async () => {
    rememberTestCareer();
    serveCareer(current);
    serveSlots({ slots: [empty(1), empty(2), empty(3)] });
    const received = captureSaves(() =>
      HttpResponse.json({ slots: [empty(1), filled(2, 5), empty(3)] }),
    );
    const { user } = renderApp(`/kariera/${TEST_CAREER_ID}/zapisy`);

    await user.click(await screen.findByRole('button', { name: 'Zapisz w slocie 2' }));

    expect(await screen.findByText('Zapisano w slocie 2.')).toBeInTheDocument();
    expect(received).toEqual([{ slot: 2, body: { expectedVersion: 9 } }]);
    const slots = within(screen.getByRole('list', { name: 'Sloty zapisu' })).getAllByRole(
      'listitem',
    );
    expect(slots[1]).toHaveTextContent(/Tydzień: 5/u);
    expect(slots[1]).toHaveTextContent(/42/u);
    expect(slots[0]).toHaveTextContent('Pusty');
  });

  it('asks before overwriting a filled slot', async () => {
    rememberTestCareer();
    serveCareer(current);
    serveSlots({ slots: [filled(1, 2), empty(2), empty(3)] });
    const received = captureSaves(() =>
      HttpResponse.json({ slots: [filled(1, 5), empty(2), empty(3)] }),
    );
    const { user } = renderApp(`/kariera/${TEST_CAREER_ID}/zapisy`);

    await user.click(await screen.findByRole('button', { name: 'Zapisz w slocie 1' }));
    expect(screen.getByText(/Slot 1 ma już zapis/u)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Anuluj' }));
    expect(received).toEqual([]);

    await user.click(screen.getByRole('button', { name: 'Zapisz w slocie 1' }));
    await user.click(screen.getByRole('button', { name: 'Tak, nadpisz' }));

    expect(await screen.findByText('Zapisano w slocie 1.')).toBeInTheDocument();
    expect(received).toHaveLength(1);
  });

  it('restores after confirmation and shows the restored career', async () => {
    rememberTestCareer();
    serveCareer(current);
    serveSlots({ slots: [filled(1, 2), empty(2), empty(3)] });
    const restored = careerView({ version: 10, week: 2, lastSettledWeek: 1 });
    const received = captureRestores(() => HttpResponse.json(actionOutcome(restored)));
    const { user, queryClient } = renderApp(`/kariera/${TEST_CAREER_ID}/zapisy`);

    await user.click(await screen.findByRole('button', { name: 'Wczytaj slot 1' }));
    expect(screen.getByText(/Postęp, którego nie masz w żadnym slocie, przepadnie/u)).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'Tak, wczytaj' }));

    expect(await screen.findByText('Wczytano slot 1. Kariera wraca do tygodnia 2.')).toBeVisible();
    expect(received[0]?.slot).toBe(1);
    expect(received[0]?.body.expectedVersion).toBe(9);
    expect(received[0]?.body.commandId).toMatch(/^[0-9a-f-]{36}$/u);
    expect(received[0]?.authorization).toBe('Bearer secret-1');
    expect(queryClient.getQueryData(getGetCareerQueryKey(TEST_CAREER_ID))).toMatchObject({
      version: 10,
    });
  });

  it('explains a stale restore', async () => {
    rememberTestCareer();
    serveCareer(current);
    serveSlots({ slots: [filled(1, 2), empty(2), empty(3)] });
    captureRestores(() => problem(409, 'stale-version'));
    const { user } = renderApp(`/kariera/${TEST_CAREER_ID}/zapisy`);

    await user.click(await screen.findByRole('button', { name: 'Wczytaj slot 1' }));
    await user.click(screen.getByRole('button', { name: 'Tak, wczytaj' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/zmieniła się w innej karcie/u);
  });

  it('downloads the export as a named JSON file', async () => {
    rememberTestCareer();
    serveCareer(current);
    serveSlots({ slots: [empty(1), empty(2), empty(3)] });
    const file = {
      format: 'polski-streamer-simulator-save',
      schema: 1,
      exportedAt: 'x',
      nickname: 'Streamer',
      state: {},
    };
    server.use(http.get('/v1/careers/:id/export', () => HttpResponse.json(file)));
    const blobs: Blob[] = [];
    Object.assign(URL, {
      createObjectURL: vi.fn((blob: Blob) => {
        blobs.push(blob);
        return 'blob:save';
      }),
      revokeObjectURL: vi.fn(),
    });
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => null);
    const { user } = renderApp(`/kariera/${TEST_CAREER_ID}/zapisy`);

    await user.click(await screen.findByRole('button', { name: 'Eksportuj zapis' }));

    await waitFor(() => {
      expect(click).toHaveBeenCalledOnce();
    });
    expect((click.mock.contexts[0] as HTMLAnchorElement).download).toBe(
      'kariera-streamer-tydzien-5.json',
    );
    expect(JSON.parse((await blobs[0]?.text()) ?? '')).toEqual(file);
    click.mockRestore();
  });

  it('tells a ranked career it has no manual saves', async () => {
    rememberTestCareer();
    serveCareer({ ...current, ranked: true, season: 1 });
    renderApp(`/kariera/${TEST_CAREER_ID}/zapisy`);

    expect(await screen.findByText(/Kariera rankingowa nie ma zapisów ręcznych/u)).toBeVisible();
    expect(screen.queryByRole('button', { name: /Zapisz w slocie/u })).not.toBeInTheDocument();
  });

  it('is linked from the dashboard of an unranked career only', async () => {
    rememberTestCareer();
    serveCareer(current);
    renderApp(`/kariera/${TEST_CAREER_ID}`);

    expect(await screen.findByRole('link', { name: 'Zapisy' })).toHaveAttribute(
      'href',
      `/kariera/${TEST_CAREER_ID}/zapisy`,
    );
  });
});

const created: CreatedCareer = {
  careerId: '0199e3a0-0000-7000-8000-000000000099',
  secret: 'secret-imported',
  recoveryKey: 'pss1.abc.def',
  career: careerView({ careerId: '0199e3a0-0000-7000-8000-000000000099', nickname: 'Kopia' }),
};

function captureImports(respond: () => Response) {
  const received: unknown[] = [];
  server.use(
    http.post('/v1/careers/import', async ({ request }) => {
      received.push(await request.json());
      return respond();
    }),
  );
  return received;
}

describe('import screen', () => {
  it('imports a file as a new career and shows its recovery key', async () => {
    const received = captureImports(() => HttpResponse.json(created, { status: 201 }));
    serveCareer(created.career);
    const { user, router } = renderApp('/importuj');
    const file = new File(
      ['{"format":"polski-streamer-simulator-save","schema":1}'],
      'zapis.json',
      {
        type: 'application/json',
      },
    );

    await user.upload(await screen.findByLabelText('Plik zapisu (.json)'), file);
    await user.click(screen.getByRole('button', { name: 'Importuj' }));

    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/kariera/${created.careerId}/klucz`);
    });
    expect(received).toEqual([{ save: { format: 'polski-streamer-simulator-save', schema: 1 } }]);
    expect(knownCareer(created.careerId)).toMatchObject({
      secret: 'secret-imported',
      recoveryKey: 'pss1.abc.def',
    });
    expect(await screen.findByText('pss1.abc.def')).toBeVisible();
  });

  it.each([
    ['import-newer', /nowszej wersji gry/u],
    ['import-unsupported', /których ten serwer nie ma/u],
    ['import-invalid', /uszkodzony albo zmieniony/u],
  ])('explains %s', async (code, message) => {
    captureImports(() => problem(422, code));
    const { user } = renderApp('/importuj');

    await user.upload(
      await screen.findByLabelText('Plik zapisu (.json)'),
      new File(['{}'], 'zapis.json', { type: 'application/json' }),
    );
    await user.click(screen.getByRole('button', { name: 'Importuj' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(message);
  });

  it('refuses a file that is not JSON without sending it', async () => {
    const received = captureImports(() => HttpResponse.json(created, { status: 201 }));
    const { user } = renderApp('/importuj');

    await user.upload(
      await screen.findByLabelText('Plik zapisu (.json)'),
      new File(['hello'], 'zapis.json', { type: 'application/json' }),
    );
    expect(await screen.findByRole('alert')).toHaveTextContent(/To nie jest plik zapisu/u);
    await user.click(screen.getByRole('button', { name: 'Importuj' }));

    expect(screen.getByRole('alert')).toHaveTextContent(/Najpierw wybierz plik/u);
    expect(received).toEqual([]);
  });

  it('is linked from the home screen', async () => {
    renderApp('/');

    expect(await screen.findByRole('link', { name: 'Importuj zapis' })).toHaveAttribute(
      'href',
      '/importuj',
    );
  });
});
