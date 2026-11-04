import { defineConfig } from 'orval';

// Generates the typed API client from the OpenAPI document Erp.Api writes on every build
// (../openapi/Erp.Api.json). Regenerate with `npm run api:generate`; never edit the output.
export default defineConfig({
  erp: {
    input: {
      target: '../openapi/Erp.Api.json',
    },
    output: {
      mode: 'split',
      target: 'src/api/generated/erp-api.ts',
      schemas: 'src/api/generated/model',
      client: 'axios-functions',
      clean: true,
      override: {
        // Every call goes through the shared axios instance (base URL, access token, refresh on 401).
        mutator: {
          path: 'src/api/http.ts',
          name: 'apiRequest',
        },
      },
    },
  },
});
