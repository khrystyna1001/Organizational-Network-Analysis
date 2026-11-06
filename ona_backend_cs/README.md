# ONA: Organization Network Analyzer

ONA is a .NET 10 solution for exploring organizational structures and networks. The solution separates the Event Storming workflow into bounded contexts. The current implementation provides an in-memory Graph API and an Organization API for importing CSV data; Identity, Analysis, and Insights are architectural scaffolds without business workflows.

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

`Domain` contains business rules and has no external package or project dependencies. `Application` defines use cases and ports; `Infrastructure` implements external adapters (persistence, identity, LLM); `Presentation` hosts HTTP APIs and composes the service. Bounded contexts must not reference another context's projects. Integrations between contexts are planned through versioned messages/API contracts, not direct code dependencies.

The current and target project layout is:

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
│   ├── ONA.Graph.Infrastructure/  # Current in-memory store; future MySQL persistence
│   └── ONA.Graph.Presentation/    # ASP.NET Core controllers, middleware, OpenAPI
├── Organization/
│   ├── ONA.Organization.Domain/         # Reserved for organization domain rules
│   ├── ONA.Organization.Application/    # CSV reader interface and import service
│   ├── ONA.Organization.Infrastructure/ # CSV parsing; future MySQL persistence
│   └── ONA.Organization.Presentation/   # CSV upload endpoint and OpenAPI
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

Each Presentation application is its own ASP.NET Core host and composition root. Graph and Organization have implemented business endpoints. Identity has an API host only; Analysis and Insights have API scaffolds only.

### Event Storming workflow and integration boundaries

| Bounded context | Event Storming commands and events | Architectural responsibility |
| --- | --- | --- |
| **Identity** | `Register User` → `Registered` | Own registration/account lifecycle and adapt the external OAuth/authorization system. The board's invalid-credentials parallel event belongs here. |
| **Organization** | `Import Hierarchy` → `Organization Hierarchy Imported`; `Ingest Communication Logs` → `Communication Logs Ingested` / `Communication Logs Processed` | Validate imports, normalize organization/communication data, and apply an agreed privacy policy before persisting or publishing data. |
| **Graph** | `Build Network Graph` → `Network Graph Built`; `Update Network Graph` → `Communication Channels Optimized` and `Graph Version Saved` | Own the graph aggregate and its version history. Build/update from accepted organization data; do not make Identity, Analysis, or Insights depend directly on Graph project types. |
| **Analysis** | `Run Graph Analysis` → `Metrics Calculated`; `Calculate Metrics` → `Metrics Updated`; `Detect Bottlenecks` → `Management Bottlenecks Identified` | Consume a graph snapshot/version and produce deterministic metrics and bottleneck findings. |
| **Insights** | `Generate Insights` → `LLM Insight Generated`; `Deliver a Recommendation` → `Recommendation Delivered`; `Respond to LLM` → `User Response` | Combine analysis results with an LLM provider, present recommendations, and record user feedback/decisions. |

`Registration Form`, `Ingestion Status Dashboard`, `Network Graph View`, `Bottleneck Dashboard`, `LLM Insight Summary Card`, and `Response Card` are read models/UI responsibilities; they are not domain entities or server-side pages in these API projects.

The intended cross-context flow is **Identity registration → Organization imports → Graph build/version → Analysis metrics and bottlenecks → Insights recommendation and user response → approved Graph update**. Integration should use versioned events or explicit API contracts and include the graph/import version so Analysis and Insights can identify the data they evaluated. The event broker is not selected or implemented yet.

### Architecture status and decisions

| Concern | Current state | Target architecture / decision |
| --- | --- | --- |
| Persistence | Graph uses a process-local `ConcurrentDictionary`; Organization parses each uploaded CSV in memory and does not persist it. | **MySQL is selected** as the target relational database. Each bounded context should own its own database/schema and migrations, even if they initially share a MySQL server. No EF Core provider, connection string, `DbContext`, or persistent repository is configured yet. |
| Authentication | No user registration or authentication logic is implemented. | Keep identity-provider integration behind Identity Infrastructure. Provider and registration ownership remain deferred; do not store credentials until the provider/security design is chosen. |
| LLM | No LLM calls or recommendation behavior is implemented. | Keep the provider behind an Insights Application port and Infrastructure adapter. Provider selection and credentials remain deferred. |
| Communication privacy | The CSV endpoint currently returns parsed values and does not ingest communication logs as a distinct workflow. | Decide how to remove/anonymize message content while retaining necessary flow metadata before implementing ingestion. |
| Graph refresh | No graph import/rebuild workflow exists. | Decide between real-time and scheduled/batch graph updates. Keep the trigger/scheduling mechanism outside the Graph domain model. |
| Metrics and LLM quality | Metrics, bottleneck detection, and LLM evaluation are not implemented. | Define how administrative-task activity is excluded from bottleneck signals and how the system detects LLM misinterpretation of graph metrics. Treat analysis results as authoritative inputs; recommendations should retain provenance to the graph version and metrics used. |
| Recommendation response | No user-response workflow or graph version history exists. | Record response/decision and preserve prior graph versions. Only an explicit approved change should lead to a Graph update; whether disagreement triggers a new recommendation or ends the workflow is still a domain decision. |

The Analysis, Insights, and Identity Infrastructure projects are empty architectural seams, not functioning adapters. MySQL and external providers can be added there without adding dependencies to Domain or references between bounded contexts.

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

The current automated tests cover Graph domain rules and application behavior in `tests/Graph`.

## Run the services

Run each command from this directory. The `http` launch profile sets `ASPNETCORE_ENVIRONMENT=Development`, where the OpenAPI document and Scalar API explorer are available.

```bash
# Graph API
dotnet run --project src/Services/Graph/ONA.Graph.Presentation/ONA.Graph.Presentation.csproj --launch-profile http

# Organization CSV API
dotnet run --project src/Services/Organization/ONA.Organization.Presentation/ONA.Organization.Presentation.csproj --launch-profile http

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
| Graph | `http://localhost:5056` | `https://localhost:7089` | `/scalar/v1` | `/openapi/v1.json` |
| Organization | `http://localhost:5025` | `https://localhost:7197` | `/scalar/v1` | `/openapi/v1.json` |
| Analysis | `http://localhost:5139` | `https://localhost:7134` | `/scalar/v1` | `/openapi/v1.json` |
| Insights | `http://localhost:5095` | `https://localhost:7144` | `/scalar/v1` | `/openapi/v1.json` |
| Identity | `http://localhost:5187` | `https://localhost:7187` | `/scalar/v1` | `/openapi/v1.json` |

OpenAPI and Scalar routes are enabled only in the Development environment. The Graph API health endpoint is `GET http://localhost:5056/` and returns a small JSON status response.

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

### Organization API

Base URL: `http://localhost:5025`

| Method | Route | Content type | Success | Error responses |
| --- | --- | --- | --- | --- |
| `POST` | `/api/v1/data` | `multipart/form-data` | `200 OK` with parsed CSV data | `400 Bad Request` |

Upload one CSV file using the form field `file`. Example:

```bash
curl -X POST http://localhost:5025/api/v1/data \
  -F "file=@../archive/employee_data.csv"
```

The response contains an import status, row count, and records. Record properties use the CSV header names as keys; blank cells and `NaN` become `null`, integer/decimal values are parsed as JSON numbers, and other values remain strings. Duplicate header names are suffixed with `.1`, `.2`, and so on.

```json
{
  "status": "success",
  "total_rows": 1,
  "data": [
    {
      "EmpID": 1,
      "FirstName": "Alex",
      "ExitDate": null
    }
  ]
}
```

The exact record shape depends on the uploaded file's CSV headers.

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

The `IOrganizationDataReader` interface reads an input `Stream` and returns parsed records. The `CsvOrganizationDataReader` implementation uses the first row as the header, preserves unique header text, fills short rows with `null`, and rejects rows containing more values than headers.

### Application interfaces

| Interface | Responsibility |
| --- | --- |
| `IGraphService` | Application operations for creating, listing, reading, updating, and deleting boards, nodes, and connections. Controllers depend on this interface. |
| `IBoardRepository` | Graph persistence abstraction: list/read/save/delete boards. Implemented by the in-memory repository. |
| `IOrganizationDataReader` | Organization CSV-reading abstraction, implemented by the infrastructure CSV parser. |

The Graph service is registered as scoped; its in-memory store and repository are singletons. Organization's CSV reader is a singleton, and its import service is scoped.

`IGraphService` exposes the following asynchronous operations (each accepts an optional `CancellationToken`):

| Resource | Create/add | List | Read | Update | Delete |
| --- | --- | --- | --- | --- | --- |
| Board | `CreateBoardAsync(CreateBoardDto) -> Task<BoardDto>` | `GetBoardsAsync() -> Task<IReadOnlyList<BoardDto>>` | `GetBoardAsync(id) -> Task<BoardDto?>` | `UpdateBoardAsync(id, UpdateBoardDto) -> Task<BoardDto?>` | `DeleteBoardAsync(id) -> Task<bool>` |
| Node | `AddNodeAsync(boardId, CreateNodeDto) -> Task<NodeDto>` | `GetNodesAsync(boardId?) -> Task<IReadOnlyList<NodeDto>>` | `GetNodeAsync(id) -> Task<NodeDto?>` | `UpdateNodeAsync(id, UpdateNodeDto) -> Task<NodeDto?>` | `DeleteNodeAsync(id) -> Task<bool>` |
| Connection | `AddConnectionAsync(boardId, CreateConnectionDto) -> Task<ConnectionDto>` | `GetConnectionsAsync(boardId?) -> Task<IReadOnlyList<ConnectionDto>>` | `GetConnectionAsync(id) -> Task<ConnectionDto?>` | `UpdateConnectionAsync(id, UpdateConnectionDto) -> Task<ConnectionDto?>` | `DeleteConnectionAsync(id) -> Task<bool>` |

`IBoardRepository` provides `GetAllAsync`, `GetByIdAsync`, `SaveAsync`, and `DeleteAsync` for board persistence. Missing reads/updates return `null` at the service boundary; delete operations return whether the resource existed. `IOrganizationDataReader.ReadCsv(Stream)` returns parsed records, and `OrganizationDataService.ReadCsv(Stream)` wraps them with their row count.

## Error responses

Graph domain errors use `application/problem+json`:

- `404 Not Found`: requested board/node/connection does not exist, or a connection references a missing board/node.
- `422 Unprocessable Entity`: a domain invariant is violated, such as an empty required field, invalid weight, self-link, or duplicate directed connection.
- Unexpected Graph errors are logged and returned as `500 Internal Server Error` Problem Details.
- Invalid/missing request fields are rejected by ASP.NET Core model validation with `400 Bad Request`.

Domain Problem Details include a machine-readable `code` extension. Organization upload errors (missing file or invalid/unreadable CSV) return `400 Bad Request`.

## Sample data

The repository's `../archive` directory contains sample CSV files that can be uploaded to the Organization API:

- `employee_data.csv`
- `employee_engagement_survey_data.csv`
- `training_and_development_data.csv`
- `recruitment_data.csv`

The Organization endpoint accepts arbitrary CSV headers; these sample column names are data examples, not a fixed API schema.
