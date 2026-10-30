import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';

/**
 * HTTP-level API double for component tests; each test adds its handlers with `server.use`. The home screen's season
 * notice (M4-F) reads `/v1/seasons` on every visit, so an empty list is the default answer.
 */
export const server = setupServer(http.get('/v1/seasons', () => HttpResponse.json([])));
