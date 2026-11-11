# ADR-005: Create a Graph board from employee CSV data

* **Status:** Accepted
* **Date:** 2026-11-10
* **Project context:** ONA Graph service

## 1. Context and quality scenario

The existing `POST /api/v1/data` endpoint parsed arbitrary CSV and returned records, but did not create domain data. The user wants an employee CSV upload to create a persisted Board, employee Nodes, and supervisor Connections. The repository's `employee_data.csv` provides `EmpID`, `FirstName`, `LastName`, `Title`, `DepartmentType`, and `Supervisor` fields that support this first graph-import workflow.

**Quality scenario — turn valid employee hierarchy data into a consistent Board:**

* **Source and stimulus:** An authorized client uploads an employee CSV using the Graph API.
* **Artifact:** A new Board aggregate and its employee Nodes and supervisor Connections.
* **Environment:** Normal operation with Graph MySQL available.
* **Expected response:** The API persists the complete graph, returns its board ID and entity counts, and preserves the prior parsed-row response data.
* **Response measure:** API integration tests verify successful graph creation/retrieval, supervisor direction, preserved parsed rows, and rejection without creating a board for invalid references.

## 2. Considered options

1. **Continue returning parsed CSV rows only**
   * **Advantage:** Maintains a generic parser with no assumptions about source schema.
   * **Disadvantage:** Does not create usable graph data or fulfill the requested workflow.

2. **Support the known employee/supervisor schema and build a Board**
   * **Advantage:** Defines a deterministic mapping from existing source data to Nodes and Connections while preserving the endpoint and existing response fields.
   * **Disadvantage:** The endpoint no longer accepts arbitrary schemas for successful board creation; future source types need explicit mapping and validation.

3. **Infer nodes and edges from arbitrary headers**
   * **Advantage:** Appears flexible for varied input files.
   * **Disadvantage:** Relationship semantics and node attributes cannot be reliably inferred from column names or sample values; invalid graphs could be created silently.

## 3. Decision

We choose **Option 2**. The compatible `POST /api/v1/data` endpoint accepts the multipart `file` and an optional `board_name`. Without a supplied board name, it uses the uploaded filename without its extension, falling back to `Imported organization` when that name is blank.

The required CSV headers are `EmpID`, `FirstName`, `LastName`, `Title`, `DepartmentType`, and `Supervisor` (header comparison is case-insensitive). Each data row creates one Node:

* Source employee key: `EmpID` (used to resolve supervisor references; the public Node ID remains service-generated).
* Node name: trimmed `FirstName` + space + trimmed `LastName`.
* Node title: `Title`.
* Node department: `DepartmentType`.
* A non-blank `Supervisor` must match another row's `EmpID`; it creates one directed `Supervisor → employee` Connection with weight `1.0`.
* A blank `Supervisor` creates no Connection.

Before creating or saving the Board, validate all required fields, unique employee IDs, and supervisor references. Reject the entire upload on missing required row values, duplicate IDs, unresolved supervisor references, or self-supervision; do not save partial data. Missing required headers or malformed CSV return `400 Bad Request`; validly parsed CSV with invalid employee/domain data returns `422 Unprocessable Entity`.

The API retains `status`, `total_rows`, and the parsed `data` response fields, and adds `board_id`, `nodes_created`, and `connections_created`. The graph is committed through one repository save; the original upload/import batch is not persisted. Other CSV schemas (including the current survey, recruitment, and training examples) are not supported for board creation by this endpoint.

## 4. Consequences

### Benefits (+)

* A successful upload immediately creates a usable, durable graph.
* Supervisor references cannot silently point to missing nodes, and invalid uploads do not create partial boards.
* Existing clients retain the route, multipart `file` field, parsed row count, and parsed record data.
* The deterministic mapping can be exercised end-to-end against the production MySQL provider.

### Costs and risks (-)

* This route is now schema-specific; new source schemas require explicit mapping decisions and tests.
* Supervisor references must identify an employee in the same file. External managers or hierarchy data split across files are rejected and need a later workflow.
* Import status/history, raw source storage, communication-log processing, anonymization, and graph versioning are not included.
* IDs are matched as trimmed, case-sensitive strings. If real source identifiers require another normalization policy, define it explicitly before changing the contract.
