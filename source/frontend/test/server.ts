import { setupServer } from 'msw/node';

/** HTTP-level API double for component tests; each test adds its handlers with `server.use`. */
export const server = setupServer();
