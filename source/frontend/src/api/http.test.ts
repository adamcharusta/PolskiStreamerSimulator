import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { server } from '../../test/server';
import { apiRequest, ApiRequestError, isApiError } from './http';

/** The rejection of a request that must fail. */
const failure = (request: Promise<unknown>): Promise<unknown> =>
  request.then(
    () => null,
    (error: unknown) => error,
  );

describe('apiRequest', () => {
  it('returns the body and keeps the content type when a career adds its secret', async () => {
    let seen: { auth: string | null; type: string | null } = { auth: null, type: null };
    server.use(
      http.post('/v1/careers/abc/actions', ({ request }) => {
        seen = {
          auth: request.headers.get('authorization'),
          type: request.headers.get('content-type'),
        };
        return HttpResponse.json({ ok: true });
      }),
    );

    const body = await apiRequest<{ ok: boolean }>(
      {
        url: '/v1/careers/abc/actions',
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        data: {},
      },
      { headers: { Authorization: 'Bearer s3cret' } },
    );

    expect(body).toEqual({ ok: true });
    expect(seen).toEqual({ auth: 'Bearer s3cret', type: 'application/json' });
  });

  it('turns Problem Details into a coded error with field errors', async () => {
    server.use(
      http.post('/v1/careers', () =>
        HttpResponse.json(
          {
            status: 400,
            code: 'validation-failed',
            detail: null,
            errors: { nickname: ['too short'] },
          },
          { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );

    const error = await failure(apiRequest({ url: '/v1/careers', method: 'POST', data: {} }));

    expect(isApiError(error)).toBe(true);
    expect(error).toMatchObject({
      status: 400,
      code: 'validation-failed',
      errors: { nickname: ['too short'] },
      isNetwork: false,
    });
  });

  it('gives a body without a code a code from its status', async () => {
    server.use(http.get('/v1/careers/x', () => new HttpResponse('nope', { status: 502 })));

    const error = (await failure(
      apiRequest({ url: '/v1/careers/x', method: 'GET' }),
    )) as ApiRequestError;

    expect(error.code).toBe('http-502');
  });

  it('marks a failed connection as a network error', async () => {
    server.use(http.get('/v1/careers/x', () => HttpResponse.error()));

    const error = (await failure(
      apiRequest({ url: '/v1/careers/x', method: 'GET' }),
    )) as ApiRequestError;

    expect(error).toMatchObject({ status: null, code: 'network-error', isNetwork: true });
  });
});
