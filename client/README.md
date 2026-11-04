# ERP client

Quasar (Vue 3 + TypeScript) app for web (SPA) and desktop (Electron). See ../CLAUDE.md for the rules.

```bash
npm install
npm run dev            # web, http://localhost:9000
npm run dev:electron   # desktop
npm run api:generate   # regenerate src/api/generated from ../openapi/Erp.Api.json
npm run lint:check
npm run typecheck
npm run build          # web; `npx quasar build -m electron` for desktop
```

Start the API first (`dotnet run --project src/Erp.Api` from the repo root); the dev server proxies `/api` to it.
