import { fileURLToPath } from 'node:url';

import { defineConfig } from 'vitest/config';

// Unit tests for client logic that does not need a browser (form rules, the auth interceptor).
// Pages are checked by lint and type check; run with `npm run test`.
export default defineConfig({
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  test: {
    include: ['src/**/*.test.ts'],
    environment: 'node',
  },
});
