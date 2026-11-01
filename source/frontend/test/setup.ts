import '@testing-library/jest-dom/vitest';

import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';

import i18n from '../src/i18n';
import { useTextFilters } from '../src/textFilters/textFilters';
import { server } from './server';

// jsdom does not implement scrolling; the router scrolls to the top after each navigation.
window.scrollTo = () => {};

// Spec §5 (#35): every player-facing key exists in Polish. A lookup that names its own fallback (defaultValue) is an
// intended fallback; any other missing key fails the test that rendered it.
const missingKeys: string[] = [];
i18n.options.saveMissing = true;
i18n.options.missingKeyHandler = (_lngs, _ns, key, _fallback, _update, options) => {
  if ((options as { defaultValue?: unknown } | undefined)?.defaultValue === undefined) {
    missingKeys.push(key);
  }
};

beforeAll(() => {
  // MSW 3 uses onUnhandledFrame for unmatched HTTP requests as well as WebSocket frames.
  server.listen({ onUnhandledFrame: 'error' });
});

afterEach(() => {
  const missing = missingKeys.splice(0);
  if (missing.length > 0) {
    throw new Error(`Missing Polish translation keys: ${missing.join(', ')}`);
  }
  server.resetHandlers();
  cleanup();
  localStorage.clear();
  // The text filters live in a module store; every test starts with both off, as a new player does.
  useTextFilters.setState({ streamerFriendly: false, sensitive: false });
});

afterAll(() => {
  server.close();
});
