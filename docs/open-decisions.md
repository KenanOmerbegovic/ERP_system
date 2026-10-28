# Open decisions

Deferred choices, each resolved by the first feature that needs it (not batched).
When a feature hits a trigger below, decide it in that feature (ask if unclear),
then remove the row and record the outcome in code/docs.

| Item | Trigger | Current state | Likely when |
|---|---|---|---|
| Business date ("today" in Europe/Sarajevo) | First document with a document date (invoice, receipt) | `IClock` exposes only `UtcNow`; never use `DateTime.Today` | Phase 1 |
| Currency list in code vs. configuration table | Companies need to enable or configure their own currencies | ISO currencies hardcoded in `Currency` (BAM, EUR, USD, CHF, GBP, RSD) | Phase 3 or never |
| Keep or remove `Erp.Integration.Tests` | First integration test (resolves: keep), or a decision not to test against real PostgreSQL (remove project) | Empty scaffold project; its reference to `Erp.Api` is unused; `dotnet test` reports "No test is available" | Phase 1 |
| Keep or remove `Erp.Seeder` → `Erp.Modules.Core` reference | Seeder starts creating Core data via Application commands (resolves: keep), or the seeder is dropped (remove) | Unused; `Program.cs` is `return 0;`. CLAUDE.md requires the seeder to use Core's commands | Next seeder feature |
| Keep or remove `coverlet.collector` | Code coverage gets collected, e.g. in CI (resolves: keep), or a decision not to measure coverage (remove from `tests/Directory.Build.props`) | Template default; only active with `dotnet test --collect "XPlat Code Coverage"`, which nothing runs | When CI is set up |

## Resolved

- **Error kinds** (Core module): `Error.Type` is `Failure | Validation | NotFound | Conflict`, mapped to
  HTTP 422 / 400 / 404 / 409. `ValidationError` carries error codes per property; clients turn codes into UI text.
- **EF Core construction** (Core module): `Entity` / `AggregateRoot` have a protected parameterless
  constructor for materialization; entities add a private one. Properties use private setters.
