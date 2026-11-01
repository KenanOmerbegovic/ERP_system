# ERP — instructions for Claude Code

## What this is
Modular monolith ERP in C# / .NET 10. The client app in /client (see Client) talks to
Erp.Api over HTTPS. PostgreSQL, EF Core, one DbContext and schema per module.
Target market: Bosnia and Herzegovina. UI language: Bosnian. Code: English.

## Architecture rules (enforced by Erp.Architecture.Tests — never weaken them)
- Modules reference other modules only via their .Contracts project.
- Cross-module communication = integration events through the outbox.
- Inside a module: Endpoints -> Application -> Domain. Domain depends only on SharedKernel.
  Infrastructure (EF Core DbContext, mappings) implements Application interfaces; Application never depends on Infrastructure.
- Country rules live only in Erp.Localization.BiH behind module-defined interfaces.
- No hardcoded rates, accounts, or numbering. Use configuration tables (effective-dated).

## Client
One Quasar (Vue 3 + TypeScript) app in /client, built for web (SPA) and
desktop (Electron). Same code for both targets. Mobile (Capacitor) may be
added later — do not add it or mobile-specific code now.
- Platform-specific code only in client/src/platform/ behind one interface.
- API client generated from Erp.Api's OpenAPI spec; never hand-write DTOs.
- Tokens: access token in memory; refresh token in platform secure storage. Never localStorage.
- Electron: contextIsolation on, nodeIntegration off.
- All layouts responsive.

## Domain rules
- Documents: Draft -> Confirmed. Confirmed documents are immutable; correct via storno.
- Money: use the Money type, decimal only, explicit rounding. Default currency KM (BAM).
- Every entity has CompanyId. Only exception: platform-level identity records in the Identity
  module (users, refresh tokens, registration keys); users reach company data through a Membership.
- Posting rules for each document type are described in docs/posting-rules.md.

## Data
- Mock data comes only from tools/Erp.Seeder, via Application commands.
- Never add test or demo data inside src/. Never special-case mock data.
- After changing posting or stock logic, run the seeder self-checks.

## Conventions
- Feature folders: Modules/X/Application/Invoices/CreateInvoice.cs (command + handler + validator).
- FluentValidation for input, Result<T> for expected failures, exceptions only for bugs.
- Async all the way, CancellationToken on every public async method.
- Tests: every new command gets unit tests; posting and tax logic gets example-based tests.

## Workflow
- Work on one feature per session. Read the module's existing code before adding to it.
- Before finishing: dotnet build (zero warnings), dotnet test, then the seeder (--size small).
- If a domain rule is unclear, ask instead of guessing.
- Before starting a feature, check docs/open-decisions.md for items it triggers; resolve them in that feature.


## Local environment
- Database: `docker compose up -d` starts PostgreSQL (pgvector image) on localhost:5433
  (not 5432: a native Windows PostgreSQL service uses that port on this machine).
- Dev connection string: Host=localhost;Port=5433;Database=erp_dev;Username=erp;Password=erp_dev_password
- Dev credentials are for local use only; never use them outside Development.
- First company: `dotnet run --project src/Erp.Api -- issue-registration-key` prints a key; register with it
  (`POST /api/registration`, then `POST /api/companies`). See src/Erp.Api/Erp.Api.http.
- `dotnet test` needs Docker running (Erp.Integration.Tests uses Testcontainers).