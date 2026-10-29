import { screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import type { RecoverCareerRequest } from '@/api/generated/game.schemas';
import { knownCareer } from '@/careers/knownCareers';
import { beginCreation } from '@/careers/pendingCreation';

import { problem, serveCareer } from '../../../test/api';
import { careerView } from '../../../test/fixtures';
import { rememberTestCareer, renderApp, TEST_CAREER_ID } from '../../../test/render';
import { server } from '../../../test/server';

const KEY = 'pss1.0199e3a0000070008000000000000001.abcdefghijklmnopqrstuvwxyzABCDEFGHIJ0123-_6';
const NEW_KEY =
  'pss1.0199e3a0000070008000000000000001.ZYXWVUTSRQPONMLKJIHGFEDCBAzyxwvutsrqpo9876-_5';

function captureRecovery(respond: () => Response) {
  const bodies: RecoverCareerRequest[] = [];
  server.use(
    http.post('/v1/careers/recover', async ({ request }) => {
      bodies.push((await request.json()) as RecoverCareerRequest);
      return respond();
    }),
  );
  return bodies;
}

describe('recovery key screen', () => {
  it('shows the stored key with copy, download and the way into the career', async () => {
    rememberTestCareer({ recoveryKey: KEY });
    const { user } = renderApp(`/kariera/${TEST_CAREER_ID}/klucz`);
    // After renderApp: userEvent.setup() installs its own clipboard.
    const writeText = vi.spyOn(navigator.clipboard, 'writeText');

    expect(await screen.findByRole('status', { name: 'Klucz odzyskiwania' })).toHaveTextContent(
      KEY,
    );
    await user.click(screen.getByRole('button', { name: 'Kopiuj' }));

    expect(writeText).toHaveBeenCalledWith(KEY);
    expect(await screen.findByText('Skopiowano klucz.')).toBeInTheDocument();
    expect(screen.getByText(/kto go ma, przejmuje karierę/u)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Zapisałem klucz — graj' })).toHaveAttribute(
      'href',
      `/kariera/${TEST_CAREER_ID}`,
    );
  });

  it('asks to copy by hand when the clipboard is denied', async () => {
    rememberTestCareer({ recoveryKey: KEY });
    const { user } = renderApp(`/kariera/${TEST_CAREER_ID}/klucz`);
    vi.spyOn(navigator.clipboard, 'writeText').mockRejectedValue(new Error('denied'));

    await user.click(await screen.findByRole('button', { name: 'Kopiuj' }));

    expect(await screen.findByText(/skopiuj go ręcznie/u)).toBeInTheDocument();
  });

  it('downloads the key as a text file', async () => {
    rememberTestCareer({ recoveryKey: KEY });
    const blobs: Blob[] = [];
    const createObjectURL = vi.fn((blob: Blob) => {
      blobs.push(blob);
      return 'blob:key';
    });
    Object.assign(URL, { createObjectURL, revokeObjectURL: vi.fn() });
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => null);
    const { user } = renderApp(`/kariera/${TEST_CAREER_ID}/klucz`);

    await user.click(await screen.findByRole('button', { name: 'Pobierz plik' }));

    expect(click).toHaveBeenCalledOnce();
    expect(await blobs[0]?.text()).toContain(KEY);
    expect(await blobs[0]?.text()).toContain('Odzyskaj karierę');
    click.mockRestore();
  });

  it('issues a key for a career created without one and stores it', async () => {
    rememberTestCareer();
    const seen: (string | null)[] = [];
    server.use(
      http.post('/v1/careers/:id/recovery-key', ({ request }) => {
        seen.push(request.headers.get('authorization'));
        return HttpResponse.json({ careerId: TEST_CAREER_ID, recoveryKey: NEW_KEY });
      }),
    );
    const { user } = renderApp(`/kariera/${TEST_CAREER_ID}/klucz`);

    expect(
      await screen.findByText('Ta kariera nie ma jeszcze klucza odzyskiwania.'),
    ).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Utwórz klucz odzyskiwania' }));

    expect(await screen.findByRole('status', { name: 'Klucz odzyskiwania' })).toHaveTextContent(
      NEW_KEY,
    );
    expect(seen).toEqual(['Bearer secret-1']);
    expect(knownCareer(TEST_CAREER_ID)?.recoveryKey).toBe(NEW_KEY);
    expect(screen.getByRole('button', { name: 'Wygeneruj nowy klucz' })).toBeInTheDocument();
  });

  it('reports a failed key request', async () => {
    rememberTestCareer();
    server.use(http.post('/v1/careers/:id/recovery-key', () => problem(500, 'internal-error')));
    const { user } = renderApp(`/kariera/${TEST_CAREER_ID}/klucz`);

    await user.click(await screen.findByRole('button', { name: 'Utwórz klucz odzyskiwania' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Nie udało się utworzyć klucza.');
  });

  it('points to recovery for a career this browser does not know', async () => {
    renderApp(`/kariera/${TEST_CAREER_ID}/klucz`);

    expect(await screen.findByText('Ta przeglądarka nie zna tej kariery.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Odzyskaj karierę kluczem' })).toBeInTheDocument();
  });
});

describe('recovery', () => {
  it('opens the career with its key and stores the new secret', async () => {
    const bodies = captureRecovery(() =>
      HttpResponse.json({ careerId: TEST_CAREER_ID, secret: 'new-secret', career: careerView() }),
    );
    serveCareer(careerView());
    const { user, router } = renderApp('/odzyskaj');

    await user.type(await screen.findByLabelText('Klucz odzyskiwania'), `  ${KEY} `);
    await user.click(screen.getByRole('button', { name: 'Odzyskaj' }));

    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/kariera/${TEST_CAREER_ID}`);
    });
    expect(bodies).toEqual([{ recoveryKey: KEY }]);
    expect(knownCareer(TEST_CAREER_ID)).toMatchObject({ secret: 'new-secret', recoveryKey: KEY });
  });

  it('reads the key from a key file', async () => {
    const { user } = renderApp('/odzyskaj');
    const file = new File([`Klucz\n${KEY}\nWklej go.`], 'klucz.txt', { type: 'text/plain' });

    await user.upload(await screen.findByLabelText('Plik z kluczem'), file);

    await waitFor(() => {
      expect(screen.getByLabelText('Klucz odzyskiwania')).toHaveValue(KEY);
    });
  });

  it('says when a file holds no key', async () => {
    const { user } = renderApp('/odzyskaj');

    await user.upload(
      await screen.findByLabelText('Plik z kluczem'),
      new File(['nic tu nie ma'], 'notatka.txt', { type: 'text/plain' }),
    );

    expect(await screen.findByRole('alert')).toHaveTextContent('W tym pliku nie ma klucza');
  });

  it('rejects text that is not a key before sending anything', async () => {
    const bodies = captureRecovery(() => HttpResponse.json({}));
    const { user } = renderApp('/odzyskaj');

    await user.type(await screen.findByLabelText('Klucz odzyskiwania'), 'pss1.nope');
    await user.click(screen.getByRole('button', { name: 'Odzyskaj' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('zaczyna się od „pss1.”');
    expect(bodies).toEqual([]);
  });

  it.each([
    ['recovery-failed', 404, 'Mogła wygasnąć'],
    ['rate-limited', 429, 'Za dużo prób'],
    ['internal-error', 500, 'Nie udało się odzyskać kariery'],
  ])('explains a %s answer', async (code, status, text) => {
    captureRecovery(() => problem(status, code));
    const { user } = renderApp('/odzyskaj');

    await user.type(await screen.findByLabelText('Klucz odzyskiwania'), KEY);
    await user.click(screen.getByRole('button', { name: 'Odzyskaj' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(text);
  });

  it('does not send a key when this browser cannot store the career', async () => {
    const bodies = captureRecovery(() => HttpResponse.json({}));
    const { user } = renderApp('/odzyskaj');
    await user.type(await screen.findByLabelText('Klucz odzyskiwania'), KEY);
    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('Storage disabled', 'SecurityError');
    });
    try {
      await user.click(screen.getByRole('button', { name: 'Odzyskaj' }));

      expect(await screen.findByRole('alert')).toHaveTextContent('Nie można zapisać dostępu');
      expect(bodies).toEqual([]);
    } finally {
      setItem.mockRestore();
    }
  });
});

describe('finishing a pending creation', () => {
  const request = {
    nickname: 'Zaległy',
    background: 'dormRoomNewcomer',
    difficulty: 'normal',
    pronouns: 'woman',
  } as const;

  it('sends the same creation ID once from the home screen and opens the key', async () => {
    const pending = beginCreation(request);
    const bodies: unknown[] = [];
    server.use(
      http.post('/v1/careers', async ({ request: sent }) => {
        bodies.push(await sent.json());
        return HttpResponse.json(
          {
            careerId: TEST_CAREER_ID,
            secret: 'late-secret',
            recoveryKey: KEY,
            career: careerView({ nickname: 'Zaległy' }),
          },
          { status: 201 },
        );
      }),
    );
    const { router } = renderApp('/');

    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/kariera/${TEST_CAREER_ID}/klucz`);
    });
    expect(bodies).toEqual([{ ...request, creationId: pending?.creationId }]);
    expect(knownCareer(TEST_CAREER_ID)).toMatchObject({ secret: 'late-secret', recoveryKey: KEY });
    expect(await screen.findByRole('status', { name: 'Klucz odzyskiwania' })).toHaveTextContent(
      KEY,
    );
  });

  it('points to recovery when the server no longer repeats the creation', async () => {
    beginCreation(request);
    server.use(http.post('/v1/careers', () => problem(409, 'creation-closed')));
    renderApp('/');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Nie da się już dokończyć zakładania kariery Zaległy',
    );
    expect(screen.getAllByRole('link', { name: 'Odzyskaj karierę kluczem' })).not.toHaveLength(0);
    expect(localStorage.getItem('pss.pendingCreation.v1')).toBeNull();
  });

  it('offers a retry while the server cannot be reached', async () => {
    beginCreation(request);
    let calls = 0;
    server.use(
      http.post('/v1/careers', () => {
        calls += 1;
        return calls <= 3
          ? HttpResponse.error()
          : HttpResponse.json(
              { careerId: TEST_CAREER_ID, secret: 's', recoveryKey: KEY, career: careerView() },
              { status: 201 },
            );
      }),
    );
    const { user, router } = renderApp('/');

    await user.click(await screen.findByRole('button', { name: 'Spróbuj ponownie' }));

    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/kariera/${TEST_CAREER_ID}/klucz`);
    });
  });
});
