# ADR-001: Define Graph aggregate boundaries

* **Status:** Proposed
* **Date:** 2026-11-09
* **Project context:** ONA (Organization Network Analyzer), Graph bounded context

> The separate Organization service described in some original context examples was removed by [ADR-003](./ADR-003-single-graph-service-with-csv-import.md). The Board aggregate boundary in this ADR remains applicable; the current CSV import endpoint is part of the Graph service.

## 1. Context and quality scenario

ONA builds organizational network graphs from imported organization data. The Graph service owns the CSV import API as well as boards, nodes, and directed connections; no separate Organization service is part of the current architecture. A connection is valid only when both endpoint nodes belong to the same board; self-links and duplicate directed links are forbidden. Removing a node must also remove its incident connections. Analysis consumes a graph snapshot, while Insights recommendations remain a separate capability.

**Quality scenario — graph consistency under concurrent commands:**

* **Source and stimulus:** Graph API clients concurrently submit node and connection changes.
* **Artifact:** A `Board` and its `Node` and `Connection` entities.
* **Environment:** Normal operation with multiple graph commands in flight.
* **Expected response:** Every accepted command leaves the board internally consistent; invalid or conflicting commands are rejected without partial mutation.
* **Response measure:** After every committed command, each connection endpoint exists in the same board, no self-links or duplicate directed pairs exist, and deleting a node leaves no incident connections. Measure command latency (including p95) under representative load; the project has not yet established a numeric latency SLO.

The Graph implementation keeps nodes and connections inside the `Board` aggregate and enforces these rules there. Boards, nodes, and connections are persisted by the Graph-owned MySQL repository and migrations described in [ADR-004](./ADR-004-graph-mysql-persistence.md). Versioned graph snapshots and the event-broker implementation remain future work.

## 2. Considered options

1. **One large aggregate spanning Board, Organization import, analysis, and recommendation data**
   * **Advantage:** A single transaction could update all related state immediately.
   * **Disadvantages:** It crosses bounded-context ownership, grows with imports and analysis history, couples unrelated writes, and makes independent context evolution or service extraction difficult.

2. **A focused Graph aggregate with cross-aggregate references by identifier only**
   * `Board` is the aggregate root and owns its `Node` and `Connection` entities.
   * Future import, `GraphVersion`, `AnalysisRun`, and `Recommendation` workflows have their own aggregate boundaries. Import functionality remains in the Graph service; other aggregates refer to them using typed IDs and versioned contracts, not object navigation properties.
   * **Advantage:** Board-level graph invariants are guarded in one place, while unrelated workflows have separate ownership and transaction boundaries.
   * **Disadvantages:** Cross-context work is not one ACID transaction; version publication, analysis, and recommendation workflows need explicit coordination, retries, and provenance.

3. **Anemic CRUD model with consistency checks in controllers or application services**
   * **Advantage:** Minimal domain-model structure for basic CRUD.
   * **Disadvantages:** Multiple entry paths can bypass or duplicate graph rules, and state changes can leave graph relationships inconsistent.

## 3. Decision

We choose **Option 2**. `Board` is the consistency boundary for graph structure: callers modify nodes and connections only through the aggregate root, and repositories load and save the aggregate rather than exposing independent write repositories for its child entities. References to entities outside the Board boundary use strongly typed identifiers; cross-context communication uses versioned events or explicit API contracts.

`GraphVersion` is an immutable snapshot with its own identity and a `BoardId`. Analysis runs refer to the specific version they analyze. A published graph is not edited in place; an approved update produces a successor version. Coordination across separate aggregates must not be represented as a cross-context object graph or assumed to be a single database transaction.

Keep the Board boundary while graph-wide rules require immediate consistency. Measure aggregate size, write contention, and command latency as graph scale grows. If those measurements make the aggregate a bottleneck, revisit the boundary with an explicit design for preserving endpoint validation and node-deletion cleanup.

## 4. Consequences

### Benefits (+)

* The aggregate root is the single gateway for rules involving nodes and connections, including same-board endpoints, no self-links, no duplicate directed connections, and cleanup of incident connections on node removal.
* Changes to unrelated imports, analyses, and recommendations do not require loading or locking the Board aggregate.
* Typed IDs and immutable graph-version references protect provenance and prevent later graph changes from silently changing the input to an earlier analysis.
* The boundary follows the existing bounded-context ownership and supports persistence or deployment changes without sharing domain classes across contexts.

### Costs and risks (-)

* Separate aggregate changes cannot rely on one global ACID transaction. The application workflow must handle intermediate states, retries, duplicate message delivery, and failures when coordinating version creation or downstream analysis.
* Keeping all nodes and connections inside one Board may increase load size and contention for very large or frequently updated graphs; representative load measurements are needed before setting a numeric performance target.
* Immutable snapshots require storage, retention, and cleanup policies, which have not yet been decided.
* The proposed lifecycle, persistent repositories, and cross-context integration are target design choices and are not all implemented in the current services.
