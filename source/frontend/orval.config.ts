import { defineConfig } from 'orval';

// Generated from the committed API contract; CI regenerates it and fails on any difference (TD-92, TD-100).
export default defineConfig({
  game: {
    input: { target: '../backend/WebApp/openapi/v1.json' },
    output: {
      mode: 'split',
      target: 'src/api/generated/game.ts',
      client: 'react-query',
      httpClient: 'axios',
      clean: true,
      override: { mutator: { path: 'src/api/http.ts', name: 'apiRequest' } },
    },
  },
});
