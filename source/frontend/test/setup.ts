import '../src/i18n';
import '@testing-library/jest-dom/vitest';

import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';

import { server } from './server';

// jsdom does not implement scrolling; the router scrolls to the top after each navigation.
window.scrollTo = () => {};

beforeAll(() => {
  // MSW 3 uses onUnhandledFrame for unmatched HTTP requests as well as WebSocket frames.
  server.listen({ onUnhandledFrame: 'error' });
});

afterEach(() => {
  server.resetHandlers();
  cleanup();
  localStorage.clear();
});

afterAll(() => {
  server.close();
});
