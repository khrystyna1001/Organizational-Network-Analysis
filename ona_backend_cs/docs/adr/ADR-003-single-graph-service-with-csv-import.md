# ADR-003: Keep CSV import in the Graph service

* **Status:** Accepted
* **Date:** 2026-11-10
* **Project context:** ONA Graph API

## 1. Context and quality scenario

The project previously had separate Organization and Graph projects, followed by an intermediate shared-host project. The user requested only one Graph service, with the existing Organization CSV API included in it and implemented in a single code file. The required compatibility contract is `POST /api/v1/data` with a multipart `file` field and the existing parsed-row response.

**Quality scenario — preserve CSV import while consolidating to one service:**

* **Source and stimulus:** A client uploads a CSV file to the existing Organization import route.
* **Artifact:** The Graph HTTP API and its in-memory CSV parsing path.
* **Environment:** Normal operation through the Graph host.
* **Expected response:** The Graph API parses and returns rows using the same response shape and parser behavior; Graph CRUD remains available from that host.
* **Response measure:** Existing CSV cases and Graph API regression checks pass, and the solution contains no separate Organization or OrganizationGraph projects.

## 2. Considered options

1. **Keep the separate Organization service and the combined host**
   * **Advantage:** Maintains the earlier project boundaries and shares a runtime.
   * **Disadvantage:** Leaves multiple services/hosts and does not meet the request for only one Graph service.

2. **Move CSV endpoint and parsing into Graph Presentation, then remove Organization and OrganizationGraph projects**
   * **Advantage:** Leaves one Graph API host and preserves the existing import route without cross-project references.
   * **Disadvantage:** CSV import parsing now lives in Graph Presentation alongside HTTP composition; richer import workflows or persistence may later justify extracting a Graph-owned application abstraction.

3. **Delete all CSV import functionality and expose only graph CRUD**
   * **Advantage:** Smallest Graph surface.
   * **Disadvantage:** Removes an existing API capability and forces clients using `POST /api/v1/data` to migrate or lose functionality.

## 3. Decision

We choose **Option 2**. The Graph Presentation `Program.cs` now contains the `POST /api/v1/data` controller endpoint and CSV parsing implementation. The endpoint keeps the previous multipart field, response shape, duplicate-header suffixing, blank-row handling, numeric parsing, and malformed/oversized-row errors.

The separate `src/Services/Organization` and `src/Services/OrganizationGraph` projects/directories and their solution entries are removed. `src/Services/Graph/ONA.Graph.Presentation` is the sole host for graph and CSV APIs, using the existing Graph ports `5056` (HTTP) and `7089` (HTTPS). At the time this ADR was accepted, CSV uploads were parsed and returned without creating a graph. The later employee-CSV-to-board behavior is specified in [ADR-005](./ADR-005-create-board-from-employee-csv.md); communication-log privacy processing and graph version history remain unimplemented.

ADR-002's shared-host decision is superseded by this ADR. The Board aggregate boundary decision in ADR-001 remains in effect for Graph domain invariants.

## 4. Consequences

### Benefits (+)

* One Graph service hosts both graph CRUD and CSV upload APIs.
* Existing clients can continue calling `POST /api/v1/data`.
* No separate Organization projects or Organization-to-Graph references are required.

### Costs and risks (-)

* CSV parsing resides in the Graph Presentation `Program.cs`, as requested, rather than behind a dedicated Organization application/infrastructure abstraction.
* The original implementation returned parsed data only. Subsequent import-to-board behavior is documented separately in ADR-005.
* Future schema validation, privacy processing, or durable import workflows will require growing this file or a deliberate follow-up design inside the Graph service.
