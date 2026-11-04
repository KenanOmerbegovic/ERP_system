# Open decisions

Deferred choices, each resolved by the first feature that needs it (not batched).
When a feature hits a trigger below, decide it in that feature (ask if unclear),
then remove the row and record the outcome in code/docs.

| Item | Trigger | Current state | Likely when |
|---|---|---|---|
| Business date ("today" in Europe/Sarajevo) | First document with a document date (invoice, receipt) | `IClock` exposes only `UtcNow`; never use `DateTime.Today` | Phase 1 |
| Currency list in code vs. configuration table | Companies need to enable or configure their own currencies | ISO currencies hardcoded in `Currency` (BAM, EUR, USD, CHF, GBP, RSD) | Phase 3 or never |
| Keep or remove `coverlet.collector` | Code coverage gets collected, e.g. in CI (resolves: keep), or a decision not to measure coverage (remove from `tests/Directory.Build.props`) | Template default; only active with `dotnet test --collect "XPlat Code Coverage"`, which nothing runs | When CI is set up |
| How the operator issues registration keys | Onboarding more than a handful of companies, or the grand administrator (below) | Operator runs `dotnet Erp.Api.dll issue-registration-key [--days N]` on the server; the key is printed once and passed to the company by hand | Before first real customers |
| Grand administrator (operator overview of all companies) | Operator needs to see or support companies, or issue keys from a UI | None. No account can see across companies; there is no list of all companies (`GET /api/companies` was removed) | After MVP |
| Existing account joining another company | A person needs access to two companies (e.g. an accounting office) | Logins are global and emails unique system-wide, but `CreateUser` only creates new accounts; an email in use elsewhere is `User.DuplicateEmail`. Needs a consent/invitation flow so one company can't attach another company's user | After MVP |
| Forgotten password / admin password reset | First user who forgets their password; first email-sending feature | Only `change-password` for the signed-in user exists. A company admin cannot reset a member's password | Phase 1 |
| Electron production origin for CORS | First packaged Electron build | In dev, web (`:9000`) and Electron (`:9300`) call `/api` through the Quasar dev-server proxy (same origin); both origins are also in `Cors:AllowedOrigins`. A packaged app loads from `file://`, where a relative `/api` cannot work: build with `ERP_API_URL`, decide the scheme (`file://` or a custom protocol) and add its origin to `Cors:AllowedOrigins` | Electron packaging |
| Production hosting of the web client | First deployment | Web builds call `/api` on their own origin (`API_BASE_URL` empty), so the API must be served under the same origin (reverse proxy) or the build sets `ERP_API_URL` and the origin goes into `Cors:AllowedOrigins`. A cross-site API would need the refresh cookie's `SameSite=Strict` revisited | First deployment |
| Auth rate limit counts refreshes | An office with several users behind one public IP, or users with many tabs | `identity-auth` (10 requests/min per IP) covers login, refresh and registration. Each tab start or reload is one refresh, so a busy office can hit 429; the client then treats the session as lost and shows the login page. Options: separate (higher) limit for refresh, per-user partition for refresh | Before first real customers |
| Logout in other tabs | Users report staying signed in in another tab | Logout revokes the refresh token, but other open tabs keep their in-memory access token until it expires (≤ 15 min) or they refresh. Could broadcast logout (BroadcastChannel) | When it matters |
| Permission codes in the client | A module adds or renames permissions | `client/src/router/permissions.ts` repeats the codes from `CorePermissions` / `IdentityPermissions`, because the OpenAPI document does not carry them. Could expose them as an enum schema in the OpenAPI document so orval generates them | Next module with permissions |
| Client test runner | First client logic worth unit testing beyond lint/type check (e.g. the auth interceptor) | None: the client has lint and type check only; the shell was verified by hand in Chrome and Electron. Likely Vitest (Quasar's `@quasar/testing-unit-vitest`) | Next client feature |
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
- **Seeder references** (seeder stage 1): `Erp.Seeder` keeps its Core reference and adds Identity and Erp.Localization.BiH;
  it composes the modules like Erp.Api and creates everything through their Application commands. Mock data comes from
  Bogus with a fixed seed. Seeded users have no default password: it is passed with `--password` (or `ERP_SEED_PASSWORD`).
  It writes only to local databases; `--reset` drops the core and identity schemas first.
- **Concurrent refresh from several browser tabs** (client shell): the client refreshes single-flight within a tab
  (one shared promise) and across tabs (Web Locks, lock `erp-session-refresh`), so a tab only refreshes after another
  tab's rotation has updated the cookie. No server-side reuse grace period.
- **Integration tests** (Identity feature): `Erp.Integration.Tests` is kept; it hosts the API with `WebApplicationFactory`
  on a Testcontainers PostgreSQL (`pgvector/pgvector:pg17`), so `dotnet test` needs Docker running.
