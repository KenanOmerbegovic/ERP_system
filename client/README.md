# ERP client

Quasar (Vue 3 + TypeScript) app for web (SPA) and desktop (Electron). See ../CLAUDE.md for the rules.

```bash
npm install
npm run dev            # web, http://localhost:9000
npm run dev:electron   # desktop
npm run api:generate   # regenerate src/api/generated from ../openapi/Erp.Api.json
npm run lint:check
npm run typecheck
npm run build          # web (dist/spa)
npx quasar build -m electron --publish never   # installer for this OS (dist/electron/Packaged)
```

Electron and electron-builder are in `src-electron/package.json` (installed there on first Electron run, or
`npm ci` in `src-electron`). CI builds the installers for every OS: `.github/workflows/client-desktop.yml`.

Start the API first (`dotnet run --project src/Erp.Api` from the repo root); the dev server proxies `/api` to it.
