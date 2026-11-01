# Open decisions

Deferred choices, each resolved by the first feature that needs it (not batched).
When a feature hits a trigger below, decide it in that feature (ask if unclear),
then remove the row and record the outcome in code/docs.

| Item | Trigger | Current state | Likely when |
|---|---|---|---|
| Business date ("today" in Europe/Sarajevo) | First document with a document date (invoice, receipt) | `IClock` exposes only `UtcNow`; never use `DateTime.Today` | Phase 1 |
| Currency list in code vs. configuration table | Companies need to enable or configure their own currencies | ISO currencies hardcoded in `Currency` (BAM, EUR, USD, CHF, GBP, RSD) | Phase 3 or never |
| Keep or remove `Erp.Seeder` → `Erp.Modules.Core` reference | Seeder starts creating Core data via Application commands (resolves: keep), or the seeder is dropped (remove) | Unused; `Program.cs` is `return 0;`. CLAUDE.md requires the seeder to use Core's commands. Seeding a company now takes Identity too: issue a key, `RegisterAccount`, then `CreateCompany` | Next seeder feature |
| Keep or remove `coverlet.collector` | Code coverage gets collected, e.g. in CI (resolves: keep), or a decision not to measure coverage (remove from `tests/Directory.Build.props`) | Template default; only active with `dotnet test --collect "XPlat Code Coverage"`, which nothing runs | When CI is set up |
| How the operator issues registration keys | Onboarding more than a handful of companies, or the grand administrator (below) | Operator runs `dotnet Erp.Api.dll issue-registration-key [--days N]` on the server; the key is printed once and passed to the company by hand | Before first real customers |
| Grand administrator (operator overview of all companies) | Operator needs to see or support companies, or issue keys from a UI | None. No account can see across companies; there is no list of all companies (`GET /api/companies` was removed) | After MVP |
| Existing account joining another company | A person needs access to two companies (e.g. an accounting office) | Logins are global and emails unique system-wide, but `CreateUser` only creates new accounts; an email in use elsewhere is `User.DuplicateEmail`. Needs a consent/invitation flow so one company can't attach another company's user | After MVP |
| Forgotten password / admin password reset | First user who forgets their password; first email-sending feature | Only `change-password` for the signed-in user exists. A company admin cannot reset a member's password | Phase 1 |
| Concurrent refresh from several browser tabs | Client auth implementation | Two tabs refreshing with the same cookie at once: the second counts as token reuse and ends the session. Client should refresh single-flight across tabs (e.g. BroadcastChannel / Web Locks), or the server gets a short reuse grace period | Client auth feature |
| Electron production origin for CORS | First packaged Electron build | Only the dev origin `http://localhost:9000` is allowed. A packaged app loads from `file://`/custom scheme; decide the scheme and add it to `Cors:AllowedOrigins` | Electron packaging |
| Client IP behind a reverse proxy | First deployment behind a proxy or load balancer | Login rate limiting partitions by `RemoteIpAddress`; behind a proxy all clients share one partition until forwarded headers are configured | First deployment |
| Outbox with more than one API instance | Running two or more API instances | `OutboxProcessor` assumes one instance; two would both deliver a message (handlers are idempotent, but work doubles). Needs row locking (`FOR UPDATE SKIP LOCKED`) or a single dispatcher | Scaling out |

## Resolved

- **Error kinds** (Core module; extended by Identity): `Error.Type` is `Failure | Validation | NotFound | Conflict | Unauthorized`,
  mapped to HTTP 422 / 400 / 404 / 409 / 401. `ValidationError` carries error codes per property; clients turn codes into UI text.
- **EF Core construction** (Core module): `Entity` / `AggregateRoot` have a protected parameterless
  constructor for materialization; entities add a private one. Properties use private setters.
- **Users and tenants** (Identity module): a user is a global login; a `Membership` (with CompanyId) gives roles in one company.
  Users, refresh tokens and registration keys are platform-level and have no CompanyId. Each company's data is fully separate.
- **Roles and permissions** (Identity module): each module declares permission codes in its Contracts (e.g. `CorePermissions`);
  companies build roles from them; a built-in, read-only Administrator role per company grants all. Permissions are checked
  against the database on every request, so changes apply immediately; the access token carries only the user id.
- **Company registration** (Identity + Core): the operator issues a single-use, expiring registration key. Step 1
  `POST /api/registration` redeems it and creates the administrator's account; step 2 `POST /api/companies` creates the
  company; Core's `CompanyCreated` event (outbox) makes that user the company's administrator. Passwords never travel in events.
- **Integration tests** (Identity feature): `Erp.Integration.Tests` is kept; it hosts the API with `WebApplicationFactory`
  on a Testcontainers PostgreSQL (`pgvector/pgvector:pg17`), so `dotnet test` needs Docker running.
