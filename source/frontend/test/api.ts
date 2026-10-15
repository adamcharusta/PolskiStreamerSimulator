import { http, HttpResponse } from 'msw';

import type { ActionRequest, BatchRequest, CareerView } from '@/api/generated/game.schemas';

import { server } from './server';

export function problem(status: number, code: string, extra: Record<string, unknown> = {}) {
  return HttpResponse.json(
    { status, code, detail: null, ...extra },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  );
}

/** `GET /v1/careers/:id` answers with each view in turn and then keeps answering with the last one. */
export function serveCareer(...views: CareerView[]) {
  let call = 0;
  const seen: (string | null)[] = [];
  server.use(
    http.get('/v1/careers/:id', ({ request }) => {
      seen.push(request.headers.get('authorization'));
      const view = views[Math.min(call, views.length - 1)];
      call += 1;
      return HttpResponse.json(view);
    }),
  );
  return seen;
}

interface Received<T> {
  readonly body: T;
  readonly authorization: string | null;
}

function capture<T>(
  path: string,
  respond: (body: T, call: number) => Response | Promise<Response>,
) {
  const received: Received<T>[] = [];
  server.use(
    http.post(path, async ({ request }) => {
      const body = (await request.json()) as T;
      received.push({ body, authorization: request.headers.get('authorization') });
      return respond(body, received.length);
    }),
  );
  return received;
}

export const captureActions = (
  respond: (body: ActionRequest, call: number) => Response | Promise<Response>,
) => capture<ActionRequest>('/v1/careers/:id/actions', respond);

export const captureBatches = (
  respond: (body: BatchRequest, call: number) => Response | Promise<Response>,
) => capture<BatchRequest>('/v1/careers/:id/batches', respond);
