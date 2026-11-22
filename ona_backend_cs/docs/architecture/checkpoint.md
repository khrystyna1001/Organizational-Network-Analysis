# ONA project checkpoint

This file is a handoff for future coding agents. Read it together with [README.md](./README.md), the source code, and the Event Storming summary below before proposing or implementing changes.

## Project at a glance

- **Name:** ONA — Organization Network Analyzer.
- **Solution:** `ONA.sln`.
- **Platform:** .NET 10 / ASP.NET Core Web APIs.
- **Architectural style:** layered Graph service with Domain, Application, Infrastructure, and Presentation; Identity, Analysis, and Insights remain separate scaffolds.
- **Graph persistence:** Graph boards, nodes, and connections use MySQL through `MySql.EntityFrameworkCore` 10.0.12 and EF Core 10.0.14. Local development targets MySQL 8.4 in Docker. Raw CSV/import batches are not persisted, but the employee graph created by CSV upload is persisted; the other contexts have no database configured.
- **Graph runtime:** one Graph API at `src/Services/Graph/ONA.Graph.Presentation`, serving graph routes and the CSV import route on port 5056 (HTTP) / 7089 (HTTPS).
- **Organization consolidation:** the separate Organization service and intermediate OrganizationGraph host have been deleted. `POST /api/v1/data` is handled by `OrganizationImportController`; CSV parsing is in the Graph ACL (`Infrastructure/Acl`). Do not recreate the deleted projects unless requested.

## Current implementation status

| Context | Exists in solution | Implemented behavior | Not implemented yet |
|---|---|---|---|
| **Graph** | Domain, Application, Infrastructure, Presentation | `Board` aggregate with `Node` and `Connection`; board/node/connection CRUD persisted to MySQL; `POST /api/v1/data` parses the employee CSV schema, creates a persisted board with employee nodes and supervisor connections, and returns parsed data plus board ID/counts; Graph API Problem Details handling; OpenAPI and Scalar | Import-batch lifecycle/persistence, communication-log processing/privacy, graph update/version history, channel optimization, and richer hierarchy workflows |
| **Identity** | Domain, Application, Infrastructure, Presentation | Architectural scaffold; Presentation serves OpenAPI/Scalar | Registration, authentication, user lifecycle, identity-provider adapter |
| **Analysis** | Domain, Application, Infrastructure, Presentation | Architectural scaffold | Graph analysis, metrics, bottleneck detection, endpoints, tests |
| **Insights** | Domain, Application, Infrastructure, Presentation | Architectural scaffold | LLM adapter/calls, insight generation, recommendation delivery, user response, graph-update approval flow |

Analysis, Insights, and Identity Infrastructure projects are empty seams. They are not functional integrations. Analysis/Insights API hosts currently have no business endpoints; Identity has no registration/auth endpoints.

The Graph repository persists boards, nodes, and connections in MySQL. The employee CSV upload creates a board graph; raw CSV rows/import batches are not persisted. Configure `ConnectionStrings:Graph` through User Secrets or `ConnectionStrings__Graph` in the environment; migrations are applied automatically at startup. Run only the Graph host for graph and import APIs.

## Event Storming board summary

The user supplied an Event Storming image for **Organization Network Analyzer**. Sticky-note workflow:

1. **Identity / registration:** `Register user` → `Registered`; invalid credentials prevent registration. Actor: User. External system: third-party OAuth/authentication.
2. **Organization data:** `Import Hierarchy` → `Organization Hierarchy Imported`; `Ingest Communication Logs` → `Communication Logs Ingested` → `Communication Logs Processed`. Actor: HR Admin. This remains a business workflow from the Event Storming board, not a separate service/project. The current Graph service supports the employee-data CSV schema to create a board graph; communication-log processing and import-batch lifecycle are still unimplemented.
3. **Graph:** `Build Network Graph` → `Network Graph Built`; later `Update Network Graph` → `Communication Channels Optimized`; save old graph versions / version history. Actor: Graph Engine. External system shown: Graph Database. CSV upload builds and persists a basic board with employee nodes and supervisor edges; the separate versioned lifecycle/update workflows are not implemented.
4. **Analysis:** `Run Graph Analysis`, `Calculate Metrics` → `Metrics Calculated` / `Metrics Updated`; `Detect Bottlenecks` → `Managerial Bottlenecks Identified`. Actor: System.
5. **Insights and feedback:** `Generate Insights` → `LLM Insight Generated`; `Deliver a Recommendation` → `Recommendation Delivered`; `Respond to LLM` → `User Response`. Actor: LLM/User. External system: LLM Provider.
6. **Read models/UI:** Registration Form, Ingestion Status Dashboard, Network Graph View, Bottleneck Dashboard, LLM Insight Summary Card, Response Card.

Hot spots / unresolved policy decisions on the board:

- How to anonymize communication content while tracking flow metadata.
- Should the network graph update in real time or in scheduled batch processing?
- What happens if a high-connectivity score is due to administrative tasks rather than a communication bottleneck?
- How to detect that an LLM misinterpreted graph metrics?
- The board also shows the user can agree/disagree with the LLM and points toward graph versioning/update; precise disagreement, approval, and rollback semantics are not decided.

Prior architecture recommendation: UI/read models are presentation/query concerns, not domain entities. Cross-context calls should use explicit versioned contracts/events rather than project references. Analysis and recommendations should retain provenance to the exact graph version and metrics used.

## Architecture guardrails

- Keep graph management and CSV parsing in the single Graph service. The CSV import currently uses no separate Organization project or domain model.
- Every Domain project should have **zero NuGet package references and zero project references**.
- Keep HTTP status codes, ASP.NET types, database/EF types, provider SDK types, and UI concerns out of Domain.
- Use each context's Application layer for use cases and ports; Infrastructure for MySQL repositories, OAuth/identity, LLM, event transport, and other adapters; Presentation for HTTP and composition root.
- The solution currently has no event broker choice. Do not introduce one silently; ask the user or propose options and document the choice.
- Graph MySQL persistence is implemented with Oracle `MySql.EntityFrameworkCore` 10.0.12 / EF Core 10.0.14. Connection strings are supplied through User Secrets or environment configuration; never commit credentials. Migrations are applied automatically at startup. Persisted scope is currently limited to Graph boards, nodes, and connections; CSV import remains in-memory.
- Identity provider and LLM provider are both **deferred/unselected**. Never add provider credentials or hard-code secrets. Use options/secret configuration and adapter interfaces when those tasks begin.
- Do not silently decide communication anonymization, real-time-vs-batch graph refresh, bottleneck false-positive handling, LLM quality/fallback, or disagreement/approval semantics. Ask the user before implementing behavior that depends on these.
- The Graph host is the only service for graph and CSV import APIs. Keep `POST /api/v1/data` and existing Graph API contracts stable unless the task specifically changes them.
- The README is the user-facing source of architecture/API/run documentation; update it when behavior or architecture changes.

## Single Graph service consolidation checklist

The user requested one Graph service with the CSV import API incorporated in its `Program.cs`. This consolidation is implemented and verified:

- [x] Put `POST /api/v1/data` in the Graph service (parser moved to the ACL in Lab 5).
- [x] Preserve the CSV response shape, duplicate-header suffixing, blank-row handling, numeric parsing, and malformed/oversized-row errors.
- [x] Remove Organization and OrganizationGraph projects/directories and their solution entries.
- [x] Keep the Graph service on HTTP `5056` / HTTPS `7089`.
- [x] Verify Graph and CSV import routes, OpenAPI/Scalar, domain errors, and the full test suite.
- [x] Update the README and ADR history to describe the single Graph service.

The other prompts below are staged work items, not a checklist to execute automatically. They remain subject to their own scope and unresolved product decisions.

## Feature implementation backlog

This is the current feature checklist, reconciled against the [README architecture status](../README.md#architecture-status-and-decisions), the [Event Storming workflow](../README.md#event-storming-workflow-and-integration-boundaries), the [Value Object and identifier specification](./aggregates-and-invariants.md), the [Graph lifecycle and invariants](./stateDiagram-analysis.md), and [ADR-001](./adr/ADR-001-graph-aggregate-boundaries.md) / [ADR-003](./adr/ADR-003-single-graph-service-with-csv-import.md). Check an item off only after implementation, focused tests, full solution validation, and documentation updates. Keep the corresponding README and domain documents aligned whenever scope or status changes. The accepted single-Graph-service consolidation above is already complete and is not a pending feature.

### Decisions to resolve before dependent implementation

- [ ] Confirm production MySQL hosting/deployment assumptions; only local Docker MySQL 8.4 is selected so far. The Graph provider and local configuration are implemented.
- [ ] Select the Identity provider/protocol and decide whether it owns credentials or ONA does.
- [ ] Define communication-log privacy/anonymization: which person identifiers, message content, and flow metadata may be retained.
- [ ] Decide whether graph rebuilds/updates are real-time or scheduled/batch.
- [ ] Define bottleneck measures, thresholds, and how administrative activity is excluded from false positives.
- [ ] Select the LLM provider and define confidence, misinterpretation detection, timeout/failure, and safe-fallback policies.
- [ ] Define recommendation disagreement, approval, cancellation, and rollback behavior; only an explicitly approved action may request a Graph update.
- [ ] Decide whether cross-context events need a broker and select one before adding broker-specific dependencies.
- [ ] Confirm whether the Event Storming read models (registration form, ingestion status, graph view, bottleneck dashboard, insight and response cards) are in scope for this backend or require a separate UI project.
- [ ] Define graph-version retention and any rollback requirements before implementing snapshot cleanup or rollback behavior.

### Features to implement

- [x] **Graph persistence:** configure Graph-owned MySQL persistence for boards/nodes/connections, add migrations and secret/environment-based connection configuration, replace the Graph in-memory repository, and add Testcontainers persistence/migration integration tests. CSV imports remain in-memory; no other contexts are persisted.
- [x] **Employee CSV-to-board import:** `POST /api/v1/data` accepts the employee schema (`EmpID`, `FirstName`, `LastName`, `Title`, `DepartmentType`, `Supervisor`), creates one node per employee and `Supervisor → employee` edges, rejects duplicate IDs/missing or self supervisor references atomically, persists the graph, and preserves parsed CSV response fields while adding `board_id` and counts. API integration tests verify successful graph creation and invalid-import atomicity.
- [ ] **Additional import workflows in Graph:** add import-batch status/outcome tracking and communication-log ingestion only after agreeing on schema/privacy; preserve the employee CSV contract and apply the approved anonymization policy before retaining or exposing communication data.
- [ ] **DDD model specification in code:** implement the documented Value Objects and immutable operations, and strongly typed IDs for the main and planned entities (`Board`, `Node`, `Connection`, `User`, `OrganizationImport`, `GraphVersion`, `AnalysisRun`, `Recommendation`). Keep Domain free of framework/provider dependencies and align names/rules with the ubiquitous language.
- [ ] **Graph version lifecycle and advanced build/update:** extend graph construction beyond the implemented employee CSV mapping as required; implement the target Board lifecycle and guards in [stateDiagram-analysis.md](./stateDiagram-analysis.md), including immutable `GraphVersion` snapshots, update candidates, approval tied to the current version, and a successor version rather than in-place edits. Define unspecified optimization, rollback, and retention behavior before implementing it.
- [ ] **Analysis:** implement `Run Graph Analysis`, `Calculate Metrics`, and `Detect Bottlenecks` against an explicit immutable graph-version contract; retain run provenance and test metric correctness, edge cases, and the agreed bottleneck/administrative-activity policy.
- [ ] **Identity:** implement `Register User`, account lifecycle, and authentication through the selected provider; keep external identity subjects distinct from `UserId`, avoid custom credential storage unless deliberately selected, and add API and provider-adapter tests.
- [ ] **Insights and response:** implement insight generation, recommendation delivery, and user response through the selected LLM provider; retain analysis/graph-version provenance, test failures/timeouts/low confidence, and enforce the agreed approval policy before requesting a Graph update.
- [ ] **Cross-context coordination:** define versioned contracts/events and implement the agreed integration workflow without cross-context Domain references; address idempotency, retries, ordering, correlation/provenance, and outbox/inbox only as justified by the selected transport.
- [ ] **Read models and UI:** if confirmed in scope, implement the Event Storming read models/interfaces in an appropriate Presentation/UI layer; do not place UI concerns or read-model entities in Domain.
- [ ] **End-to-end verification and docs:** for each delivered workflow, add positive/negative acceptance tests, update API/OpenAPI and README/checkpoint/domain docs, and verify the full solution build and test suite.

## Useful commands and URLs

Run commands from the `ona_backend_cs` directory:

The commands and ports below describe the current runtime. Graph is the only service with implemented business APIs; Analysis, Insights, and Identity remain separate scaffold hosts.

```bash
dotnet restore ONA.sln
dotnet build ONA.sln
dotnet test ONA.sln

dotnet run --project src/Services/Graph/ONA.Graph.Presentation/ONA.Graph.Presentation.csproj --launch-profile http
dotnet run --project src/Services/Identity/ONA.Identity.Presentation/ONA.Identity.Presentation.csproj --launch-profile http
dotnet run --project src/Services/Analysis/ONA.Analysis.Presentation/ONA.Analysis.Presentation.csproj --launch-profile http
dotnet run --project src/Services/Insights/ONA.Insights.Presentation/ONA.Insights.Presentation.csproj --launch-profile http
```

Services run in separate terminals. In Development, each exposes `/openapi/v1.json` and `/scalar/v1`.

| Service | HTTP port | HTTPS port |
|---|---:|---:|
| Graph (including CSV import) | 5056 | 7089 |
| Analysis | 5139 | 7134 |
| Insights | 5095 | 7144 |
| Identity | 5187 | 7187 |

Implemented API routes:

- Graph health: `GET /`
- Graph boards: `GET/POST /api/v1/boards`, `GET/PUT/DELETE /api/v1/boards/{boardId}`, `POST /api/v1/boards/{boardId}/build|publish|archive`
- Graph nodes: `GET/POST /api/v1/nodes`, `GET/PUT/DELETE /api/v1/nodes/{nodeId}`; list can filter with `board_id`
- Graph connections: `GET/POST /api/v1/connections`, `GET/PUT/DELETE /api/v1/connections/{connectionId}`; list can filter with `board_id`
- Graph CSV import: `POST /api/v1/data`, multipart form field `file`
- Identity, Analysis, Insights: no business routes yet.

> **Lab 5 update:** Board CQRS + lifecycle (Draft → Built → Published → Archived, migration `AddBoardLifecycle`), HRIS ACL, value objects `BoardName`/`NodeName`/`DepartmentName`, and architecture tests were added. The baseline below predates them; re-run `dotnet test ONA.sln`.

## Verification baseline

At the last completed validation after adding Graph MySQL persistence:

- `dotnet build ona_backend_cs/ONA.sln --no-restore` succeeded with **0 warnings and 0 errors**.
- `dotnet test ona_backend_cs/ONA.sln --no-restore` passed **24 tests** (11 Domain, 9 Application, 4 MySQL/API integration), 0 failed.
- MySQL Testcontainers integration tests ran against MySQL 8.4 and verified migrations, persistence across DbContext scopes, incident-connection cleanup after node deletion, employee CSV-to-board creation, and rejection of invalid supervisor references without creating a board.
- The following live HTTP checks were performed before replacing the in-memory repository; they have not been repeated against the user's MySQL container.
- Graph host live checks: health and boards returned `200`; CSV upload returned `200`; OpenAPI listed graph and CSV routes.
- Error checks: invalid Graph connection referencing a missing board returned `404 application/problem+json` with `board.not_found`; CSV upload without a file returned `400`.
- Identity scaffold returned HTTP 200 for `/openapi/v1.json` and `/scalar/v1`; its OpenAPI document had zero business paths, as expected.
- Graph API live checks returned: board creation `201`, reading a board `200`, invalid node domain rule `422`, missing board `404`.
- Graph CSV import live checks returned upload `200` and missing-file `400`.

The repository worktree was not clean at checkpoint creation. The preceding architecture work had uncommitted changes to `ONA.sln`, `README.md`, Analysis/Insights Presentation references, and new Analysis/Insights Infrastructure plus Identity scaffold projects. Preserve user changes and inspect `git status` before modifying or cleaning anything.

## Suggested next tasks and ready-to-use agent prompts

These are staged prompts, not authorization to implement everything at once. Begin each task by re-reading this checkpoint, the README, and the relevant code; ask clarifying questions for unresolved domain/product choices. Implement one bounded scope at a time and run focused tests plus the full solution suite.

### Prompt 1 — Architecture consistency review

> Review the ONA solution against `checkpoint.md` and `README.md`. Do not implement business workflows or add dependencies. Verify every bounded-context project reference stays within its own context, every Domain project has no package/project references, all four current contexts (Graph, Identity, Analysis, Insights) have the intended layer projects, and docs accurately distinguish implemented behavior from scaffolds. Confirm no removed Organization service/project is described as current. Report concrete inconsistencies and propose the smallest corrections; do not make unrelated edits.

### Prompt 2 — Graph MySQL persistence (completed)

> Completed in [ADR-004](./adr/ADR-004-graph-mysql-persistence.md). Graph boards, nodes, and connections use MySQL; CSV imports remain in-memory. The committed migration and Testcontainers integration tests are part of the current solution.

### Prompt 3 — Organization data import workflows and privacy

> Extend the CSV import capability within the existing Graph service to support the hierarchy and communication-log workflows, using ubiquitous-language command/event names from `checkpoint.md`; do not recreate a separate Organization service. Before coding, ask the user to decide the anonymization policy (which fields/content may be retained) and whether uploads are synchronous or queued. Keep the existing CSV import route compatible, validate schemas explicitly, persist only after the MySQL design is available, add focused tests for valid/malformed/duplicate-header/oversized rows and privacy guarantees, document endpoints and events, and verify the full solution.

### Prompt 4 — Graph build/versioning from imports

> Implement Graph's `BuildNetworkGraph` and `UpdateNetworkGraph` application workflows from validated import data inside the Graph service. First ask/confirm how imports feed graph construction and whether refresh is real-time or scheduled. Add explicit graph-version snapshots and save-history behavior, preserve existing Graph CRUD and the CSV import API, test rebuild/update/rollback and referential invariants, update OpenAPI and README, and run all tests.

### Prompt 5 — Analysis metrics and bottleneck detection

> Implement Analysis use cases for `CalculateMetrics` and `DetectBottlenecks` over an explicit immutable Graph snapshot/version contract. Do not reference Graph Domain directly. Before implementing bottleneck rules, ask the user how administrative-task activity should be separated from communication bottlenecks and what thresholds/interpretation they expect. Add unit tests for metric correctness, edge cases, version provenance, and false-positive policy; add documented API endpoints and run the entire suite.

### Prompt 6 — Identity/registration

> Implement the Identity bounded context's `RegisterUser` workflow. First ask which identity provider/protocol is required and whether the provider owns credentials or ONA does. Do not build custom password storage or assume the OAuth provider. Define ports in Application, implement the provider adapter only after selection, keep tokens/secrets out of source, add auth/registration tests and API docs, and verify no other context directly references Identity projects.

### Prompt 7 — Insights, LLM, and response flow

> Implement Insights `GenerateInsights`, `DeliverRecommendation`, and `RespondToLLM` workflows only after confirming the LLM provider and policies for disagreement, low confidence, and safe fallback. Use an Application-level provider interface and Infrastructure adapter; do not put provider SDK types in Domain or expose secrets. Persist recommendation provenance (graph version and metrics), record user response, and require an explicit approved action before requesting a Graph update. Add deterministic tests with a fake LLM, failure/timeout/low-confidence tests, update API docs, then run all tests.

### Prompt 8 — Cross-context events and reliability

> Propose an event contract and delivery architecture for the ONA flow in `checkpoint.md`. Do not choose/install a broker without user approval. Address event versioning, idempotency, retries, ordering, outbox/inbox, correlation IDs, and graph-version provenance. Keep context projects isolated. After agreement, implement one end-to-end slice with contract tests and failure/replay tests, document operations, and validate the solution.
