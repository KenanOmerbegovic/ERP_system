# Open decisions

Deferred choices, each resolved by the first feature that needs it (not batched).
When a feature hits a trigger below, decide it in that feature (ask if unclear),
then remove the row and record the outcome in code/docs.

| Item | Trigger | Current state | Likely when |
|---|---|---|---|
| Currency list in code vs. configuration table | Companies need to enable or configure their own currencies | ISO currencies hardcoded in `Currency` (BAM, EUR, USD, CHF, GBP, RSD) | Phase 3 or never |
| How the operator issues registration keys | Onboarding more than a handful of companies, or the grand administrator (below) | Operator runs `dotnet Erp.Api.dll issue-registration-key [--days N]` on the server; the key is printed once and passed to the company by hand | Before first real customers |
| Grand administrator (operator overview of all companies) | Operator needs to see or support companies, or issue keys from a UI | None. No account can see across companies; there is no list of all companies (`GET /api/companies` was removed) | After MVP |
| Existing account joining another company | A person needs access to two companies (e.g. an accounting office) | Logins are global and emails unique system-wide, but `CreateUser` only creates new accounts; an email in use elsewhere is `User.DuplicateEmail`. Needs a consent/invitation flow so one company can't attach another company's user | After MVP |
| Forgotten password / admin password reset | First user who forgets their password; first email-sending feature | Only `change-password` for the signed-in user exists. A company admin cannot reset a member's password | Phase 1 |
| Electron production origin for CORS | First packaged Electron build used against a real API | In dev, web (`:9000`) and Electron (`:9300`) call `/api` through the Quasar dev-server proxy (same origin); both origins are also in `Cors:AllowedOrigins`. `client-desktop.yml` builds installers that load from `file://`, where a relative `/api` cannot work: the build takes `ERP_API_URL` from the repository variable (unset today, so the installers cannot sign in). Decide the scheme (`file://` or a custom protocol) and add its origin to `Cors:AllowedOrigins` | When the API gets hosted |
| Production hosting of the web client | API gets hosted | `client-web.yml` deploys the SPA to GitHub Pages (`https://<owner>.github.io/<repo>/`, hash routes). With no hosted API the page loads but sign-in fails. Web builds call `/api` on their own origin unless `ERP_API_URL` is set; Pages cannot proxy, so the API will be cross-site: the origin goes into `Cors:AllowedOrigins`, and the refresh cookie's `SameSite=Strict` (sent only same-site) must be revisited, or the SPA moves to a host that serves the API under the same site | When the API gets hosted |
| Staging branch and environment | API gets hosted | One branch (`main`); every push to `main` deploys the SPA to Pages, and nothing deploys the API. Likely: a `staging` GitHub environment (own `ERP_API_URL`, secrets and approval rule) fed from `main`, production from tags or a `release` branch, and the seeder run against staging only | When the API gets hosted |
| Code signing of desktop installers | First installer given to a customer | `client-desktop.yml` uploads unsigned installers as workflow artifacts (Windows NSIS, macOS DMG x64/arm64, Linux AppImage); SmartScreen and Gatekeeper warn, and macOS may refuse to open the app without notarization. Needs a Windows code-signing certificate and an Apple Developer ID (secrets in the repo), and a decision on publishing tags as GitHub Releases and on auto-update | Before first real customers |
| Auth rate limit counts refreshes | An office with several users behind one public IP, or users with many tabs | `identity-auth` (10 requests/min per IP) covers login, refresh and registration. Each tab start or reload is one refresh, so a busy office can hit 429; the client then treats the session as lost and shows the login page. Options: separate (higher) limit for refresh, per-user partition for refresh | Before first real customers |
| Logout in other tabs | Users report staying signed in in another tab | Logout revokes the refresh token, but other open tabs keep their in-memory access token until it expires (≤ 15 min) or they refresh. Could broadcast logout (BroadcastChannel) | When it matters |
| Permission codes in the client | A module adds or renames permissions | `client/src/router/permissions.ts` repeats the codes from `CorePermissions`, `IdentityPermissions`, `WarehousePermissions` and `PurchasingPermissions`, because the OpenAPI document does not carry them. Could expose them as an enum schema in the OpenAPI document so orval generates them | Next module with permissions |
| Client IP behind a reverse proxy | First deployment behind a proxy or load balancer | Login rate limiting partitions by `RemoteIpAddress`; behind a proxy all clients share one partition until forwarded headers are configured | First deployment |
| Outbox with more than one API instance | Running two or more API instances | `OutboxProcessor` assumes one instance; two would both deliver a message (handlers are idempotent, but work doubles). Needs row locking (`FOR UPDATE SKIP LOCKED`) or a single dispatcher | Scaling out |
| Purchasing in foreign currency | A supplier invoices in EUR etc. | Purchase orders, receipts and supplier invoices are KM only; stock unit costs are KM. Needs exchange rates (configuration, effective-dated) and a rule for which rate values the receipt | When a foreign supplier is needed |
| Back-dated stock documents and cost | Users confirm documents dated in the past, or Finance needs period-exact costs | The weighted average follows the order of confirmation, not the document date; a back-dated receipt does not revalue issues confirmed earlier. Options: forbid dates before the last confirmed movement, or recalculate | Phase 3 |
| Storno price differences | Finance posts stock documents | A storno of a receipt takes the goods out at their original value as far as the stock value allows; any rest (stock would be left negative or with value but no quantity) is reported as `PriceDifference` on the event line. Finance decides the account (price differences / COGS) | Phase 3 |
| Reserved stock | Sales orders should hold stock before delivery | Not implemented: available = on hand; nothing is reserved | Phase 2b |
| LOT expiry rules | Expired goods must not be issued, or issues should pick the earliest expiry (FEFO) | LOTs carry an optional expiry date set by their first receipt; issues name the LOT and nothing checks expiry | When needed |
| Stock counts (FR-WH-05) | FR-WH-05 is scheduled | Not implemented; a count would post its differences as receipts and issues | Later phase |
| Warehouse data for articles created before the module | A database that existed before Warehouse is upgraded instead of reseeded | Warehouse learns about articles only from Core's `ArticleCreated`/`ArticleUpdated` events; older articles are unknown to it until they are edited. Dev databases are reseeded with `--reset`; a real upgrade would need Core to republish its articles | First real upgrade |
| Purchase price tolerance | Invoices often differ from the order price | A supplier invoice may charge any price; the difference per line is reported as `PriceDifference` (see Resolved). No tolerance and no approval step for large differences | Phase 3 |
| Service purchases | Buying services (transport, maintenance) or other non-stock costs | Order lines must be goods; an invoice needs confirmed receipt lines. Needs order lines without receipts, or invoices without an order | Phase 3 |
| Supplier-stated PDV differs from the computed PDV | A supplier's printed PDV differs by rounding | The invoice's PDV is computed (per line, rounded to fening); the supplier's own figure is not recorded. Options: record it and accept differences up to a few fening | Phase 3 |
| Non-deductible input PDV | Purchases whose input PDV cannot be deducted (e.g. representation) | All input PDV on supplier invoices is treated alike; Finance would need a deductible/non-deductible flag per VAT code or line | Phase 3 |
| Shared number-series code | Sales repeats the pattern | Warehouse and Purchasing each have their own `number_series` and counters (same shape, duplicated code), as each module owns its configuration. Could move to a shared building block once a third module needs it | Phase 2c |
| Receipts left "Confirming" | The API stops between the warehouse call and completing a receipt | The receipt stays Confirming (its quantity counts as pending) until someone confirms it again; the UI says so and offers to finish. There is no automatic retry | When it happens in practice |
| PDV rate lookup errors on invoice lines | Another module uses `IVatCalculator` | A missing rate is mapped to its invoice line by matching the code in the calculator's error message. Better: the calculator reports the failing code in a structured error | Phase 2c |

## Resolved

- **Warehouse command contract** (Purchasing module): `IStockDocumentCommands` in `Erp.Modules.Warehouse.Contracts` (`ReceiveAsync`, `ReverseAsync`), idempotent per source document through `warehouse.command_log` (same key and payload: the first result; same key, other payload: 409 `Command.IdempotencyKeyReused`; failures leave nothing behind). No transaction spans the two modules: Purchasing freezes a receipt as Confirming, calls Warehouse, then completes it; a repeated confirmation repeats the call and gets the same stock document. A stock document created this way is reversed only by its owning module.
- **Price differences** (Purchasing module): stock is valued at the order price when goods are received. A supplier invoice at another price does not revalue stock (the goods may already be issued at the weighted average); each invoice line records `PriceDifference` = net − received value, and `SupplierInvoiceRegistered` carries it for Finance's price-difference account.
- **PDV rates and calculation** (Purchasing module, for Sales too): rates are company configuration in `core.vat_rates` (company-defined codes, effective-dated, never edited); articles have an optional default VAT code. `IVatCalculator` (Core.Contracts) is implemented in Erp.Localization.BiH: PDV per line = net × rate rounded to fening, totals per rate are sums of lines, rates for the document date.
- **Over-receipt and over-invoicing** (Purchasing module): not allowed. A receipt line may take at most what is open on the order line (ordered − received − pending in receipts being confirmed); an invoice line at most what is received and not invoiced. The order is the lock that serializes both.
- **Synchronous commands between modules** (approved after the Warehouse module): a module may expose queries and commands as
  interfaces in its .Contracts project when the caller needs the answer at once, e.g. a stock issue that must be able to fail
  with `Stock.Insufficient` before Sales confirms its delivery note. Commands are idempotent (key: source module + source
  document id); everything else stays event-based. Events only were rejected: an issue could fail after the user was told the
  delivery note is confirmed, which needs a compensating saga. Modules still reference only other modules' .Contracts projects,
  checked in compiled assemblies (`ModuleBoundaryTests`) and in project files (`ProjectReferenceTests`).
- **Business date** (Warehouse module): `IClock.Today` is the date in the business time zone. The host builds
  `SystemClock` with `BiHLocale.TimeZone` (Europe/Sarajevo, in Erp.Localization.BiH). Stock documents may not be dated
  after it; a storno is dated with it.
- **Client test runner** (Warehouse client pages): Vitest (`npm run test`, `client/vitest.config.ts`), for client
  logic that needs no browser (stock document form rules, the auth interceptor); run in `client-web.yml`. Pages are still
  checked by lint and type check only.
- **Article tracking type** (Warehouse module): none / LOT / serial, set in Core when the article is created and fixed
  afterwards (Core cannot see stock); a service is never tracked. Warehouse keeps its own copy of articles from Core's events.
- **Stock document numbering** (Warehouse module): a number is assigned on confirmation, gapless, counted per company,
  document type and year of the document date; prefix and padding come from the effective-dated `warehouse.number_series`
  table (no defaults in code). Confirming without a series fails with `NumberSeries.NotConfigured`.
- **Concurrent stock movements** (Warehouse module, FR-WH-03): confirm, storno and draft edits lock the document, then
  the stock items, serial numbers and number counter they touch (`SELECT … FOR UPDATE`, fixed order), in one transaction;
  check constraints keep quantities and values non-negative as a last line of defence.
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
- **Keep or remove `coverlet.collector`** (CI setup): removed. CI (`.github/workflows/backend.yml`) builds and tests
  but does not measure coverage, and nothing else collected it. Add it back (`tests/Directory.Build.props`) together
  with a coverage step if coverage becomes a goal.
- **Integration tests** (Identity feature): `Erp.Integration.Tests` is kept; it hosts the API with `WebApplicationFactory`
  on a Testcontainers PostgreSQL (`pgvector/pgvector:pg17`), so `dotnet test` needs Docker running.
