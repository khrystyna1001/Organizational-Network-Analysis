# ONA: Organization Network Analyzer

ONA is a .NET 10 solution for exploring organizational structures and networks. The Graph API provides board/node/connection management and a CSV import endpoint at `POST /api/v1/data` that creates and persists a board graph from employee and supervisor data. Raw CSV parsing remains in-memory. Identity, Analysis, and Insights are architectural scaffolds without business workflows.

## Contents

- [Architecture](#architecture)
- [Event Storming workflow and integration boundaries](#event-storming-workflow-and-integration-boundaries)
- [Architecture status and decisions](#architecture-status-and-decisions)
- [Requirements](#requirements)
- [Build and test](#build-and-test)
- [Run the services](#run-the-services)
- [Useful URLs](#useful-urls)
- [API documentation](#api-documentation)
- [Model schema and interface documentation](#model-schema-and-interface-documentation)
- [Error responses](#error-responses)
- [Sample data](#sample-data)

## Architecture

The solution is organized by bounded context under `src/Services`. Each service follows inward dependency direction:

```text
Presentation -> Application -> Domain
Presentation -> Infrastructure -> Application
```

`Domain` contains business rules and has no external package or project dependencies. `Application` defines use cases and ports; `Infrastructure` implements external adapters (persistence, identity, LLM); `Presentation` hosts HTTP APIs and composes the service. The former Organization CSV import API is now part of the Graph service; its endpoint and CSV parsing are defined in the Graph Presentation `Program.cs`.

Current project layout:

```text
src/Services/
├── Identity/
│   ├── ONA.Identity.Domain/         # User registration and identity business rules (scaffold)
│   ├── ONA.Identity.Application/    # Registration use cases and identity-provider ports (scaffold)
│   ├── ONA.Identity.Infrastructure/# External OAuth/identity adapters (future)
│   └── ONA.Identity.Presentation/   # Identity API host (no business endpoints yet)
├── Graph/
│   ├── ONA.Graph.Domain/          # Board aggregate, Node/Connection entities, domain rules
│   ├── ONA.Graph.Application/     # IGraphService, DTOs, IBoardRepository
│   ├── ONA.Graph.Infrastructure/  # MySQL DbContext, migrations, and repository
│   └── ONA.Graph.Presentation/    # Graph endpoints, CSV import endpoint/parser, middleware, OpenAPI
├── Analysis/
│   ├── ONA.Analysis.Domain/
│   ├── ONA.Analysis.Application/
│   ├── ONA.Analysis.Infrastructure/ # Future graph-data adapter and analysis persistence
│   └── ONA.Analysis.Presentation/  # Scaffold; no business endpoints yet
└── Insights/
    ├── ONA.Insights.Domain/
    ├── ONA.Insights.Application/
    ├── ONA.Insights.Infrastructure/ # Future LLM provider and recommendation persistence
    └── ONA.Insights.Presentation/  # Scaffold; no business endpoints yet
```

**Current implementation:** `ONA.Graph.Presentation` is the only host for the graph and organization CSV APIs. The CSV import endpoint and its parser are colocated in its `Program.cs`; no separate Organization service/project or combined host remains. Identity has an API host only; Analysis and Insights have API scaffolds only.

**Architecture decision:** retain one Graph service and place the existing CSV import endpoint in that service, preserving `POST /api/v1/data`. The former separate Organization service and the intermediate shared-host project have been removed. See [ADR-003](./docs/adr/ADR-003-single-graph-service-with-csv-import.md).

### Event Storming workflow and integration boundaries

| Bounded context | Event Storming commands and events | Architectural responsibility |
| --- | --- | --- |
| **Identity** | `Register User` → `Registered` | Own registration/account lifecycle and adapt the external OAuth/authorization system. The board's invalid-credentials parallel event belongs here. |
| **Graph service** | CSV import: `POST /api/v1/data`; `Build Network Graph` → `Network Graph Built`; `Update Network Graph` → `Communication Channels Optimized` and `Graph Version Saved` | Serve CSV import and graph management in one service. The endpoint builds a persisted board from the supported employee/supervisor CSV schema. Communication-log processing, import-batch lifecycle/privacy handling, graph versions, and channel optimization are not implemented. |
| **Analysis** | `Run Graph Analysis` → `Metrics Calculated`; `Calculate Metrics` → `Metrics Updated`; `Detect Bottlenecks` → `Management Bottlenecks Identified` | Consume a graph snapshot/version and produce deterministic metrics and bottleneck findings. |
| **Insights** | `Generate Insights` → `LLM Insight Generated`; `Deliver a Recommendation` → `Recommendation Delivered`; `Respond to LLM` → `User Response` | Combine analysis results with an LLM provider, present recommendations, and record user feedback/decisions. |

`Registration Form`, `Ingestion Status Dashboard`, `Network Graph View`, `Bottleneck Dashboard`, `LLM Insight Summary Card`, and `Response Card` are read models/UI responsibilities; they are not domain entities or server-side pages in these API projects.

The intended workflow is **Identity registration → CSV/data import → Graph build/version → Analysis metrics and bottlenecks → Insights recommendation and user response → approved Graph update**. Analysis and Insights should identify the graph version they evaluated. The event broker is not selected or implemented yet.

### Architecture status and decisions

| Concern | Current state | Target architecture / decision |
| --- | --- | --- |
| Persistence | Graph boards, nodes, and connections are stored in MySQL through Graph-owned EF Core infrastructure and migrations. Raw CSV/import batches are not persisted; the board graph produced by the import is persisted. | Local development uses MySQL 8.4 (Docker). The application reads `ConnectionStrings:Graph` from User Secrets or `ConnectionStrings__Graph` from the environment. Identity, Analysis, and Insights have no persistence configured. |
| Authentication | No user registration or authentication logic is implemented. | Keep identity-provider integration behind Identity Infrastructure. Provider and registration ownership remain deferred; do not store credentials until the provider/security design is chosen. |
| LLM | No LLM calls or recommendation behavior is implemented. | Keep the provider behind an Insights Application port and Infrastructure adapter. Provider selection and credentials remain deferred. |
| Communication privacy | The CSV endpoint currently returns parsed values and does not ingest communication logs as a distinct workflow. | Decide how to remove/anonymize message content while retaining necessary flow metadata before implementing ingestion. |
| Graph refresh | No graph import/rebuild workflow exists. | Decide between real-time and scheduled/batch graph updates. Keep the trigger/scheduling mechanism outside the Graph domain model. |
| Metrics and LLM quality | Metrics, bottleneck detection, and LLM evaluation are not implemented. | Define how administrative-task activity is excluded from bottleneck signals and how the system detects LLM misinterpretation of graph metrics. Treat analysis results as authoritative inputs; recommendations should retain provenance to the graph version and metrics used. |
| Recommendation response | No user-response workflow or graph version history exists. | Record response/decision and preserve prior graph versions. Only an explicit approved change should lead to a Graph update; whether disagreement triggers a new recommendation or ends the workflow is still a domain decision. |
| Service topology | The Graph service hosts graph CRUD and CSV import at port 5056 (HTTP) / 7089 (HTTPS). | Keep one Graph service; CSV import remains a Graph Presentation endpoint. See [ADR-003](./docs/adr/ADR-003-single-graph-service-with-csv-import.md). |

The Analysis, Insights, and Identity Infrastructure projects are empty architectural seams, not functioning adapters. Graph persistence is implemented only for boards, nodes, and connections; uploaded CSV rows are parsed in memory and returned alongside the persisted graph identifiers/counts.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git (to clone the repository)

## Build and test

Run these commands from this directory (`ona_backend_cs`):

```bash
dotnet restore ONA.sln
dotnet build ONA.sln
dotnet test ONA.sln
```

The automated tests cover Graph domain rules and application behavior. MySQL persistence and migration integration tests use Testcontainers and require Docker; run them with:

```bash
dotnet test tests/Graph/ONA.Graph.Infrastructure.IntegrationTests/ONA.Graph.Infrastructure.IntegrationTests.csproj
```

### Configure and migrate Graph MySQL

The Graph API requires a MySQL connection string at startup. For local development, set it with User Secrets (credentials are not committed):

```bash
dotnet user-secrets set "ConnectionStrings:Graph" "Server=localhost;Port=3306;Database=ona_graph;User Id=ona;Password=your-local-password" \
  --project src/Services/Graph/ONA.Graph.Presentation/ONA.Graph.Presentation.csproj
```

Replace `your-local-password` with the value configured as `MYSQL_PASSWORD` for your local container.

For deployment, provide the equivalent `ConnectionStrings__Graph` environment variable through the hosting platform's secret configuration. Install the EF CLI if it is not already available, then apply the committed Graph migrations before starting the API:

```bash
dotnet tool install --global dotnet-ef --version 10.0.14
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update \
  --project src/Services/Graph/ONA.Graph.Infrastructure/ONA.Graph.Infrastructure.csproj \
  --startup-project src/Services/Graph/ONA.Graph.Presentation/ONA.Graph.Presentation.csproj \
  --context GraphDbContext
```

Migrations are not applied automatically at API startup. Raw CSV source rows are not stored; the Board, Nodes, and Connections created from them are persisted by the Graph repository.

## Run the services

Run each command from this directory. The `http` launch profile sets `ASPNETCORE_ENVIRONMENT=Development`, where the OpenAPI document and Scalar API explorer are available.

```bash
# Graph API (includes CSV import)
dotnet run --project src/Services/Graph/ONA.Graph.Presentation/ONA.Graph.Presentation.csproj --launch-profile http

# Analysis scaffold
dotnet run --project src/Services/Analysis/ONA.Analysis.Presentation/ONA.Analysis.Presentation.csproj --launch-profile http

# Insights scaffold
dotnet run --project src/Services/Insights/ONA.Insights.Presentation/ONA.Insights.Presentation.csproj --launch-profile http

# Identity scaffold
dotnet run --project src/Services/Identity/ONA.Identity.Presentation/ONA.Identity.Presentation.csproj --launch-profile http
```

Run services in separate terminals. Each service has distinct HTTP and HTTPS ports. To use the HTTPS profile instead, replace `--launch-profile http` with `--launch-profile https`.

## Useful URLs

| Service | HTTP | HTTPS | Interactive API explorer | OpenAPI document |
| --- | --- | --- | --- | --- |
| Graph (including CSV import) | `http://localhost:5056` | `https://localhost:7089` | `/scalar/v1` | `/openapi/v1.json` |
| Analysis | `http://localhost:5139` | `https://localhost:7134` | `/scalar/v1` | `/openapi/v1.json` |
| Insights | `http://localhost:5095` | `https://localhost:7144` | `/scalar/v1` | `/openapi/v1.json` |
| Identity | `http://localhost:5187` | `https://localhost:7187` | `/scalar/v1` | `/openapi/v1.json` |

OpenAPI and Scalar routes are enabled only in the Development environment. The Graph health endpoint is `GET http://localhost:5056/` and returns a small JSON status response. Its OpenAPI document includes graph management and CSV import endpoints.

## API documentation

Resource endpoints are versioned under `/api/v1`; the Graph health route is `GET /`. The Graph service uses `snake_case` JSON property names and query parameters. For example, `board_id` in a request corresponds to `BoardId` in the C# contract.

### Graph API

Base URL: `http://localhost:5056`

#### Health

| Method | Route | Success | Description |
| --- | --- | --- | --- |
| `GET` | `/` | `200 OK` | Returns the service status. |

#### Boards

| Method | Route | Success | Error responses |
| --- | --- | --- | --- |
| `POST` | `/api/v1/boards` | `201 Created` with the created board and `Location` header | `400`, `422` |
| `GET` | `/api/v1/boards` | `200 OK` with a board array | — |
| `GET` | `/api/v1/boards/{boardId}` | `200 OK` with a board | `404` |
| `PUT` | `/api/v1/boards/{boardId}` | `200 OK` with the updated board | `404`, `422` |
| `DELETE` | `/api/v1/boards/{boardId}` | `204 No Content` | `404` |

Create board body:

```json
{
  "name": "Engineering",
  "description": "Engineering organization network"
}
```

`name` must be non-empty. `description` is optional. On update, omitted fields are preserved; setting `description` to `null` clears it.

#### Nodes

| Method | Route | Success | Error responses |
| --- | --- | --- | --- |
| `POST` | `/api/v1/nodes` | `201 Created` with the created node and `Location` header | `400`, `404`, `422` |
| `GET` | `/api/v1/nodes` | `200 OK` with a node array | — |
| `GET` | `/api/v1/nodes/{nodeId}` | `200 OK` with a node | `404` |
| `PUT` | `/api/v1/nodes/{nodeId}` | `200 OK` with the updated node | `404`, `422` |
| `DELETE` | `/api/v1/nodes/{nodeId}` | `204 No Content` | `404` |

Use the optional `board_id` query parameter to filter a node list. Create body:

```json
{
  "board_id": "BOARD_ID",
  "name": "Alex",
  "title": "Director",
  "department": "Engineering"
}
```

All four values are required in practice and must be non-empty. Update requests may supply individual fields; fields omitted or `null` retain their existing values. A node is scoped to its board. Deleting a node also removes connections attached to it.

#### Connections

| Method | Route | Success | Error responses |
| --- | --- | --- | --- |
| `POST` | `/api/v1/connections` | `201 Created` with the created connection and `Location` header | `400`, `404`, `422` |
| `GET` | `/api/v1/connections` | `200 OK` with a connection array | — |
| `GET` | `/api/v1/connections/{connectionId}` | `200 OK` with a connection | `404` |
| `PUT` | `/api/v1/connections/{connectionId}` | `200 OK` with the updated connection | `404`, `422` |
| `DELETE` | `/api/v1/connections/{connectionId}` | `204 No Content` | `404` |

Use the optional `board_id` query parameter to filter a connection list. Create body:

```json
{
  "board_id": "BOARD_ID",
  "source_node_id": "SOURCE_NODE_ID",
  "target_node_id": "TARGET_NODE_ID",
  "weight": 1.0
}
```

`board_id`, `source_node_id`, and `target_node_id` must identify an existing board and two distinct nodes in that board. `weight` defaults to `1.0`; it may be `null` or a finite number greater than zero. Duplicate directed links are rejected. Updating a connection changes only its weight; omitting `weight` preserves it, while explicit `null` clears it.

### CSV import API (Graph service)

Base URL: `http://localhost:5056`

| Method | Route | Content type | Success | Error responses |
| --- | --- | --- | --- | --- |
| `POST` | `/api/v1/data` | `multipart/form-data` | `200 OK` with parsed CSV data and created board identifiers/counts | `400 Bad Request`, `422 Unprocessable Entity` |

Upload one employee CSV using the form field `file`. The supported headers are `EmpID`, `FirstName`, `LastName`, `Title`, `DepartmentType`, and `Supervisor`. `EmpID` is the row's unique source identifier; `Supervisor` must be blank or match another row's `EmpID`. Each row creates one node (`FirstName + LastName`, `Title`, `DepartmentType`); each non-blank supervisor creates a directed `Supervisor → employee` connection with weight `1.0`. Supervisor references are validated before saving. Invalid rows, duplicate IDs, self-supervision, or unresolved references reject the whole upload and create no board. Other CSV schemas are rejected.

An optional `board_name` multipart field sets the board name; otherwise the uploaded filename (without extension) is used. Example:

```bash
curl -X POST http://localhost:5056/api/v1/data \
  -F "board_name=Engineering" \
  -F "file=@../archive/employee_data.csv"
```

The response preserves `status`, `total_rows`, and the parsed `data`, and adds `board_id`, `nodes_created`, and `connections_created`. Record properties use the CSV header names as keys; blank cells and `NaN` become `null`, integer/decimal values are parsed as JSON numbers, and other values remain strings. Duplicate header names are suffixed with `.1`, `.2`, and so on.

```json
{
  "status": "success",
  "total_rows": 1,
  "board_id": "generated-board-id",
  "nodes_created": 1,
  "connections_created": 0,
  "data": [
    {
      "EmpID": 100,
      "FirstName": "Alex",
      "LastName": "One",
      "Title": "Director",
      "DepartmentType": "Management",
      "Supervisor": null
    }
  ]
}
```

The `data` record shape follows the uploaded CSV headers; the graph mapping uses the required employee schema listed above.

### Identity, Analysis, and Insights

These services currently expose no business controllers or API routes. Their `/openapi/v1.json` documents and `/scalar/v1` explorers are available, but contain no application endpoints yet. Identity is reserved for registration and the future external identity-provider integration; Analysis and Insights are reserved for metrics/bottlenecks and LLM/recommendation workflows, respectively.

## Model schema and interface documentation

The Graph API serializes request/response fields in `snake_case`. IDs are opaque strings generated by the service.

### Graph models

| Model | Fields | Relationships and rules |
| --- | --- | --- |
| `Board` | `id: string`, `name: string`, `description: string \| null` | Aggregate root containing nodes and connections. `name` cannot be empty. |
| `Node` | `id: string`, `board_id: string`, `name: string`, `title: string`, `department: string` | Belongs to one board. All descriptive fields are required and non-empty. |
| `Connection` | `id: string`, `board_id: string`, `source_node_id: string`, `target_node_id: string`, `weight: number \| null` | Directed edge between distinct nodes on its board. Duplicate edges in the same direction are disallowed. |

API response shapes:

```json
{
  "id": "board-id",
  "name": "Engineering",
  "description": "Engineering organization network"
}
```

```json
{
  "id": "node-id",
  "board_id": "board-id",
  "name": "Alex",
  "title": "Director",
  "department": "Engineering"
}
```

```json
{
  "id": "connection-id",
  "board_id": "board-id",
  "source_node_id": "source-node-id",
  "target_node_id": "target-node-id",
  "weight": 1.0
}
```

Create and update input shapes are documented above in the corresponding endpoint sections. Fields omitted from update requests generally retain their previous value. The create connection weight is optional and defaults to `1.0`.

### Organization import schema

`OrganizationDataResponse` has:

| Field | Type | Description |
| --- | --- | --- |
| `status` | `string` | `"success"` when parsing succeeds. |
| `total_rows` | `integer` | Number of non-empty data rows parsed (header excluded). |
| `data` | `array<object>` | Records whose keys are derived from the CSV header row. Values are `string`, `integer`, `number`, or `null`. |

The CSV endpoint and parser are implemented in the Graph Presentation `Program.cs`; employee-to-graph mapping is in the Graph Application service. The parser uses the first row as the header, makes duplicate header names unique by adding `.1`, `.2`, and so on, fills short rows with `null`, skips blank rows, and rejects rows containing more values than headers. Only the required employee schema creates a board; see the CSV import API section.

### Application interfaces

| Interface | Responsibility |
| --- | --- |
| `IGraphService` | Application operations for creating, listing, reading, updating, and deleting boards, nodes, and connections. Controllers depend on this interface. |
| `IBoardRepository` | Graph persistence abstraction: list/read/save/delete boards. Implemented by the scoped MySQL repository backed by `GraphDbContext`. |

The Graph service, MySQL repository, and `GraphDbContext` are scoped. Raw CSV rows are parsed in memory; the resulting board graph is persisted.

`IGraphService` exposes the following asynchronous operations (each accepts an optional `CancellationToken`):

| Resource | Create/add | List | Read | Update | Delete |
| --- | --- | --- | --- | --- | --- |
| Board | `CreateBoardAsync(CreateBoardDto) -> Task<BoardDto>` | `GetBoardsAsync() -> Task<IReadOnlyList<BoardDto>>` | `GetBoardAsync(id) -> Task<BoardDto?>` | `UpdateBoardAsync(id, UpdateBoardDto) -> Task<BoardDto?>` | `DeleteBoardAsync(id) -> Task<bool>` |
| Node | `AddNodeAsync(boardId, CreateNodeDto) -> Task<NodeDto>` | `GetNodesAsync(boardId?) -> Task<IReadOnlyList<NodeDto>>` | `GetNodeAsync(id) -> Task<NodeDto?>` | `UpdateNodeAsync(id, UpdateNodeDto) -> Task<NodeDto?>` | `DeleteNodeAsync(id) -> Task<bool>` |
| Connection | `AddConnectionAsync(boardId, CreateConnectionDto) -> Task<ConnectionDto>` | `GetConnectionsAsync(boardId?) -> Task<IReadOnlyList<ConnectionDto>>` | `GetConnectionAsync(id) -> Task<ConnectionDto?>` | `UpdateConnectionAsync(id, UpdateConnectionDto) -> Task<ConnectionDto?>` | `DeleteConnectionAsync(id) -> Task<bool>` |

`IBoardRepository` provides `GetAllAsync`, `GetByIdAsync`, `SaveAsync`, and `DeleteAsync` for board persistence. Missing reads/updates return `null` at the service boundary; delete operations return whether the resource existed.

## Error responses

Graph domain errors use `application/problem+json`:

- `404 Not Found`: requested board/node/connection does not exist, or a connection references a missing board/node.
- `422 Unprocessable Entity`: a domain invariant is violated, such as an empty required field, invalid weight, self-link, or duplicate directed connection.
- Unexpected Graph errors are logged and returned as `500 Internal Server Error` Problem Details.
- Invalid/missing request fields are rejected by ASP.NET Core model validation with `400 Bad Request`.

Domain Problem Details include a machine-readable `code` extension. CSV import errors (missing file, invalid/unreadable CSV, or missing required headers) return `400 Bad Request`; invalid employee rows or supervisor references return `422 Unprocessable Entity` with no board created.

## Sample data

The repository's `../archive` directory contains sample CSV files that can be uploaded to the Graph API's CSV import endpoint:

- `employee_data.csv`
- `employee_engagement_survey_data.csv`
- `training_and_development_data.csv`
- `recruitment_data.csv`

Only `employee_data.csv` matches the current CSV-to-board schema. The survey, recruitment, and training CSVs do not contain the supported employee/supervisor columns and cannot currently create a board through this endpoint.
