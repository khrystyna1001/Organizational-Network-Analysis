# AGENTS.md – ONA rules for AI agents

## 1. Sources of truth
- `docs/architecture/aggregates-and-invariants.md` – Value Objects, ids
- `docs/architecture/stateDiagram-analysis.md` – Board lifecycle, invariants, exceptions, Given–When–Then
- `docs/architecture/context-map.md` – contexts, `IEmployeeDirectoryPort`, HRIS mapping table

Use these names exactly. Missing concept → update the `.md` first.

## 2. Commands
- Build: `dotnet build ONA.sln`
- Fast tests: `dotnet test ONA.sln --filter "FullyQualifiedName!~IntegrationTests"`
- Architecture tests: `dotnet test ONA.sln --filter "FullyQualifiedName~CleanArchitectureGuardTests"`
- All tests (Docker/MySQL): `dotnet test ONA.sln`

Done = `Failed: 0`. Never weaken a test to make it pass.

## 3. Workflow: Plan → Red → Green
1. Plan: contracts only (ports, commands/queries, exceptions).
2. Red: tests from the `.md` scenarios/tables.
3. Green: minimal code.
4. Verify: `dotnet test`, review `git diff`.

## 4. Layers
- **Domain** – no packages, no references.
- **Application** – references Domain only. CQRS in `Features/<Aggregate>/{Commands,Queries}`, ports in `Ports/`.
- **Infrastructure** – EF Core (MySQL), repositories, `Acl/`. All classes `internal sealed` except `DependencyInjection` and EF migrations.
- **Presentation** – (Graph) `BoardsController` and `OrganizationImportController` call one CQRS handler via `[FromServices]`; nodes/connections still go through `IGraphService`.

CQRS: commands load tracked aggregates and call aggregate methods; queries use `AsNoTracking()` and return DTOs. No MediatR.

## 5. Domain
- No `public set` / `init`; collections as `IReadOnlyCollection<T>`.
- No ORM attributes.
- Errors: `sealed` `DomainException` subclasses with `snake_case` `Code`.

## 6. ACL
- Port uses only our types; foreign `*_DTO` types are `internal sealed` in `Acl/ExternalContracts`.
- Translator (pure, no I/O) / Adapter (I/O) / Acl (facade).
- Business errors → `DomainException`; technical errors → `HrisIntegrationException`.

## 7. Tests
- xUnit, AAA. New tests use real objects or hand-written fakes; never assert a value you set on a mock.
- Assert exception type and `Code`.
- Every ACL mapping-table row has a test.

## 8. Graph context
- `Board` is the only aggregate root; one repository: `IBoardRepository`.
- Lifecycle: Draft → Built → Published → Archived (State pattern). `UpdatePending` is not implemented.
- Published/Archived graphs are immutable; Published boards cannot be deleted.
- HRIS column names stay in `Infrastructure/Acl`.
- MySQL 8.4, `snake_case` columns; schema changes need a migration.

## 9. Other contexts
- Same layering, CQRS and domain rules as Graph. No MediatR. Kafka only for the two events in ADR-007; reads stay on HTTP.
- Every API is `[Authorize]` (JWT, shared `Jwt:*` config); only health is anonymous. Roles from the `role` claim. Consumers use a `Service` token. Events are published after the save and never fail the request; consumers are idempotent and commit only after handling.
- Without `Kafka:BootstrapServers` the publisher is a no-op and no consumer runs.
- Cross-context calls go through an Application port + `Infrastructure/Acl` HTTP adapter; contexts share ids only, never code.
- Analysis reads `GET /boards/{id}/snapshot`; Insights reads `GET /analysis-runs/{id}`.
- Each service has its own MySQL database (`ona_<context>`).
- Frontend (`frontend/`, plain HTML/CSS/JS, no build step; vis-network is vendored in `vendor/`) only calls the HTTP APIs, same-origin through the nginx proxy, which also serves the files. A new public route needs a proxy rule. Put API data into the DOM with text nodes only (see `h()` in `js/app.js`), never `innerHTML`.
