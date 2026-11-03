# EchoAPI .NET Backend

This solution scaffolds the services identified from the Organization Network Analyzer event-storming board and the existing [`ona_backend`](../ona_backend/README.md) API. The solution and projects use the Git repository name, `EchoAPI`.

## Bounded contexts

Each context is an independent service with `Domain`, `Application`, and ASP.NET Core `Presentation` projects. Contexts with I/O also have an `Infrastructure` project. Dependencies flow inward: `Application` -> `Domain`, `Infrastructure` -> `Application`, and the composition root in `Presentation` -> `Application` and `Infrastructure`. No service references another context.

| Context | Board responsibilities |
| --- | --- |
| `Organization` | Import hierarchy and communication-log data; provides the CSV ingestion API described in the Python README. |
| `Graph` | Core service: manage analysis boards, organizational nodes, and weighted connections; build and update the network graph. |
| `Analysis` | Calculate network metrics and detect management bottlenecks. |
| `Insights` | Generate insights and recommendations and handle user responses to LLM recommendations. |

`Graph` is the primary implemented context. Its in-memory repositories live in `EchoAPI.Graph.Infrastructure`; the CSV reader lives in `EchoAPI.Organization.Infrastructure`. Analysis and Insights remain scaffolds without infrastructure projects until they need I/O.

## Run

From this directory:

```bash
dotnet run --project src/Services/Graph/EchoAPI.Graph.Presentation
dotnet run --project src/Services/Organization/EchoAPI.Organization.Presentation
```

In development, each Web API exposes its OpenAPI document at `/openapi/v1.json`. The Analysis and Insights projects are service scaffolds ready for their use cases.

## API from the Python README

The route shapes, response fields, and `snake_case` JSON names are retained:

- The Graph service has board CRUD at `/api/v1/boards`, node CRUD at `/api/v1/nodes`, and connection CRUD at `/api/v1/connections`. Node and connection lists accept `?board_id=...`.
- The Organization service accepts `POST /api/v1/data` with a multipart `file` CSV and returns `status`, `total_rows`, and `data`.
- `GET /` on the Graph service reports its status.

Data is stored in memory and is lost when the service process stops, matching the current Python implementation.

The Graph Domain project defines an abstract `DomainException` with a machine-readable `Code` and concrete board, node, and connection invariant exceptions. Domain invariant violations are mapped to HTTP 422 Problem Details responses, missing boards or nodes to HTTP 404, and unexpected failures to HTTP 500.

Each running API is a composition root: it calls the context's `AddInfrastructure()` extension to register application services and technical adapters while keeping repository implementations internal.

## Build and test

```bash
dotnet build EchoAPI.sln
dotnet test EchoAPI.sln
```

The Graph context has Domain and Application xUnit test projects under `tests/Graph`; the Application tests use Moq.
