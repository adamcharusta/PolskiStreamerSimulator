import { fileURLToPath, URL } from 'node:url';

import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// Compose sets API_PROXY_TARGET to the api service and VITE_POLLING for Docker Desktop on Windows (TD-99).
const apiTarget = process.env.API_PROXY_TARGET ?? 'http://localhost:8080';
const polling = process.env.VITE_POLLING === 'true';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { '@': fileURLToPath(new URL('src', import.meta.url)) } },
  server: {
    port: 5173,
    strictPort: true,
    proxy: { '/v1': { target: apiTarget } },
    watch: polling ? { usePolling: true, interval: 300 } : undefined,
  },
});
