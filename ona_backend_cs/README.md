# ONA – Organization Network Analyzer

.NET 10 backend for analysing organization networks. Only the **Graph** service is implemented: boards with nodes and connections, a board lifecycle, and an employee CSV import, persisted in MySQL. Identity, Analysis and Insights are empty scaffolds.

Rules for code and AI agents: [`AGENTS.md`](AGENTS.md). Design docs: [`docs/architecture`](docs/architecture).

## Structure

```text
src/Services/<Context>/ONA.<Context>.{Domain,Application,Infrastructure,Presentation}
tests/Graph/   Domain, Application, Infrastructure (ACL), Architecture, Integration (MySQL) tests
```

## Run

Requirements: .NET 10 SDK, Docker.

```bash
docker run -d --name ona-mysql -e MYSQL_ROOT_PASSWORD=local_root_password -e MYSQL_DATABASE=ona_graph -p 3306:3306 mysql:8.4
dotnet run --project src/Services/Graph/ONA.Graph.Presentation --launch-profile http
```

- The connection string `ConnectionStrings:Graph` is in `appsettings.Development.json`; override it with User Secrets or `ConnectionStrings__Graph`.
- Migrations are applied automatically at startup.
- API: `http://localhost:5056`, explorer `/scalar/v1`, OpenAPI `/openapi/v1.json`.
- Example requests: `src/Services/Graph/ONA.Graph.Presentation/ONA.Graph.Presentation.http`.

## Test

```bash
dotnet test ONA.sln                                                   # all (needs Docker)
dotnet test ONA.sln --filter "FullyQualifiedName!~IntegrationTests"  # without Docker
```

## Graph API

JSON uses `snake_case`. Base path `/api/v1`.

| Resource | Endpoints |
| --- | --- |
| Boards | `GET/POST /boards`, `GET/PUT/DELETE /boards/{id}` |
| Board lifecycle | `POST /boards/{id}/build` (Draft → Built), `/publish` (Built → Published), `/archive` (Published → Archived) |
| Nodes | `GET/POST /nodes`, `GET/PUT/DELETE /nodes/{id}` (filter: `?board_id=`) |
| Connections | `GET/POST /connections`, `GET/PUT/DELETE /connections/{id}` (filter: `?board_id=`) |
| CSV import | `POST /data` (multipart: `file`, optional `board_name`) |

Main rules:
- A board can be built only with at least one node.
- Nodes and connections of a Published or Archived board cannot be changed.
- A Published board cannot be deleted; archive it first.
- Connections must link two different nodes of the same board, with weight > 0 (default `1.0`) and no duplicates in the same direction.

### CSV import

Required columns: `EmpID, FirstName, LastName, Title, DepartmentType, Supervisor`; other columns are ignored. Each row becomes a node, and each `Supervisor` becomes a connection supervisor → employee. Invalid data rejects the whole file. Full mapping: [`context-map.md`](docs/architecture/context-map.md). Sample file: `../archive/employee_data.csv`.

```bash
curl -X POST http://localhost:5056/api/v1/data -F "board_name=Engineering" -F "file=@../archive/employee_data.csv"
```

## Errors

Problem Details (`application/problem+json`) with a `code` field:

| Status | When |
| --- | --- |
| `400` | Invalid request, missing file, unreadable CSV |
| `404` | Board / node / connection not found |
| `422` | Business rule violated (e.g. `board.deletion_not_allowed`, `board_import.invalid`) |
| `500` | Unexpected error |
