# ONA – Organization Network Analyzer

.NET 10 backend plus a small web UI for analysing organization networks. Four services, each with its own MySQL database:

| Service | Port | Does |
| --- | --- | --- |
| **Graph** | 5056 | Boards, nodes, connections, lifecycle, employee CSV import, graph snapshot |
| **Analysis** | 5139 | Metrics and bottleneck detection for a published graph version |
| **Insights** | 5095 | Recommendations per bottleneck, approve / reject |
| **Identity** | 5187 | Registration and login |

Web UI: [`frontend/`](frontend) (plain HTML/CSS/JS, no build step: dark dashboard with graph, person inspector, recommendations, roster). An nginx proxy ([`nginx/nginx.conf`](nginx/nginx.conf)) is the single entry point on `http://localhost:8080`: `/api/v1/...` goes to the services, everything else to the UI.

Rules for code and AI agents: [`AGENTS.md`](AGENTS.md). Design docs: [`docs/architecture`](docs/architecture). API contracts (OpenAPI 3.1, one per service, snake_case JSON): [Graph](docs/contracts/openapi-Graph.yaml), [Analysis](docs/contracts/openapi-Analysis.yaml), [Insights](docs/contracts/openapi-Insights.yaml), [Identity](docs/contracts/openapi-Identity.yaml). In Development each service also serves its own live copy at `/openapi/v1.json` and a UI at `/scalar/v1`.

## Structure

```text
src/Services/<Context>/ONA.<Context>.{Domain,Application,Infrastructure,Presentation}
tests/<Context>/   Domain, Application, Infrastructure, Architecture tests (Graph also has MySQL integration tests)
frontend/          web UI (index.html, css/, js/, vendor/vis-network)
nginx/             reverse proxy config
sample/            valid example CSV
```

## Run

Requirements: .NET 10 SDK, Docker.

**Everything in Docker** (the proxy serves the UI and routes the APIs; Kafka, Zookeeper and Mailpit start as dependencies)

```bash
docker compose up --build mysql graph-api identity-api analysis-api insights-api proxy
```

Open `http://localhost:8080`, register (or sign in as the seeded admin `admin@ona.local` / `Admin123!`), import `sample/employees.csv`, then build → publish the board. Publishing triggers the analysis and the recommendations through Kafka; press Refresh in the UI. Published ports: proxy 8080, MySQL 3306, Mailpit 8025 (password-reset emails), Kafka 9092. The APIs are reachable as `http://localhost:8080/api/v1/{boards,nodes,connections,data,users,analysis-runs,recommendations}`. The Dockerfile uses `ubuntu:26.04` (Docker Hub) with apt-installed .NET 10, not the `mcr.microsoft.com` images (see the comment at the top of the Dockerfile). If you change a database schema, reset with `docker compose down -v` (new services use `EnsureCreated`, not migrations).

**APIs only, locally** (no Kafka by default: the publish → analysis → recommendations chain then needs the manual calls below) (MySQL in Docker, one terminal per service; the UI needs the proxy, so it runs in Docker only)

```bash
docker compose up -d mysql
dotnet run --project src/Services/Graph/ONA.Graph.Presentation --launch-profile http      # :5056
dotnet run --project src/Services/Identity/ONA.Identity.Presentation --launch-profile http # :5187
dotnet run --project src/Services/Analysis/ONA.Analysis.Presentation --launch-profile http # :5139
dotnet run --project src/Services/Insights/ONA.Insights.Presentation --launch-profile http # :5095
```

- Connection strings: `ConnectionStrings:<Graph|Identity|Analysis|Insights>` in `appsettings.Development.json`; override with User Secrets or `ConnectionStrings__<Name>`.
- Analysis calls Graph (`Services:Graph:BaseUrl`); Insights calls Analysis (`Services:Analysis:BaseUrl`).
- Databases are created at startup. Each service has `/scalar/v1` and `/openapi/v1.json` in Development.
- Example requests: `src/Services/Graph/ONA.Graph.Presentation/ONA.Graph.Presentation.http`.

## Test

```bash
dotnet test ONA.sln                                                   # all (needs Docker)
dotnet test ONA.sln --filter "FullyQualifiedName!~IntegrationTests"  # without Docker
```

CI (`.github/workflows/ci.yml`): on every push/PR to `main` it runs all tests, then builds the images and smoke-tests all four services with MySQL – only if the tests passed.

## Graph API

JSON uses `snake_case`. Base path `/api/v1`.

| Resource | Endpoints |
| --- | --- |
| Boards | `GET/POST /boards`, `GET/PUT/DELETE /boards/{id}` |
| Board lifecycle | `POST /boards/{id}/build` (Draft → Built), `/publish` (Built → Published), `/archive` (Published → Archived) |
| Nodes | `GET/POST /nodes`, `GET/PUT/DELETE /nodes/{id}` (filter: `?board_id=`) |
| Connections | `GET/POST /connections`, `GET/PUT/DELETE /connections/{id}` (filter: `?board_id=`) |
| Snapshot | `GET /boards/{id}/snapshot` – nodes and connections of the published version (used by Analysis) |
| CSV import | `POST /data/preview` (multipart `file`): columns, first rows and a suggested mapping, nothing saved. `POST /data` (multipart: `file`, optional `board_name`, optional `mapping` JSON) |

Main rules:
- A board can be built only with at least one node.
- Nodes and connections of a Published or Archived board cannot be changed.
- A Published board cannot be deleted; archive it first.
- Connections must link two different nodes of the same board, with weight > 0 (default `1.0`) and no duplicates in the same direction.

### CSV import (any people table)

One row per person, any column names. In the UI, choosing a file opens a **column mapping** step (pre-filled from common header names); over the API send the same choice as the `mapping` form field:

```json
{"employee_id": "Staff No.", "full_name": "Who", "title": "Position", "department": "Office", "supervisor": "Reports To"}
```

- Roles (all optional except a name source): `employee_id`, `full_name` **or** `first_name`/`last_name`, `title`, `department`, `supervisor`. Unmapped title/department become "Employee"/"General"; without `employee_id` people get `row-<n>` ids.
- `supervisor` may be an ID or a full name; a boss who is not in the file becomes a placeholder person (department `External`).
- **No `mapping` = the HR export format** (`EmpID, FirstName, LastName, Title, DepartmentType, Supervisor`), so older clients keep working.
- A bad row (blank name, duplicate ID, self-supervision) rejects the whole file; the message names the mapped column and the row.
- Full rules: [`context-map.md`](docs/architecture/context-map.md). Sample files: `sample/employees.csv` (small, clean) and `../archive/employee_data.csv` (3,000 rows → 5,945 nodes: 2,945 are placeholder supervisors, so the network is mostly tiny pairs).

```bash
# $TOKEN = access_token from POST /api/v1/users/login
curl -X POST http://localhost:5056/api/v1/data -H "Authorization: Bearer $TOKEN" -F "board_name=Engineering" -F "file=@sample/employees.csv"

# any other CSV: preview, then import with a mapping
curl -X POST http://localhost:5056/api/v1/data/preview -H "Authorization: Bearer $TOKEN" -F "file=@people.csv"
curl -X POST http://localhost:5056/api/v1/data -H "Authorization: Bearer $TOKEN" -F "file=@people.csv" -F 'mapping={"full_name":"Name","supervisor":"Manager"}'
```

## Authentication and roles

Identity issues a JWT (HS256) on login; every other API validates it with the shared `Jwt:Key` (override with `Jwt__Key`). Send `Authorization: Bearer <access_token>`. Roles: `User`, `Admin`, `Service` (internal, for Kafka consumers). Only `Admin` can delete boards and manage users; `Service` cannot answer recommendations.

| Service | Endpoints |
| --- | --- |
| Identity | anonymous: `POST /api/v1/users` (register), `POST /api/v1/users/login`, `POST /api/v1/users/password-reset/request` (always 202, emails a token), `POST /api/v1/users/password-reset/confirm`. Signed in: `GET/PUT /api/v1/users/me`, `POST /api/v1/users/me/password` (a wrong current password gives 401 `user.invalid_credentials`). Login returns `{access_token, token_type: "Bearer", expires_in, user}` (60 minutes by default, `Jwt:ExpiresMinutes`); the reset token is valid for 30 minutes. Registration always creates role `User`. Admin: `GET /api/v1/users[/{id}]`, `PUT /api/v1/users/{id}/role` |
| Analysis | `POST /api/v1/analysis-runs` `{board_id}` (201, always a new run, needs a published board), `GET /api/v1/analysis-runs[?board_id=]` (newest first), `GET /api/v1/analysis-runs/{id}`. A run has `status` (`Running`, `Completed`, `Failed`), `metrics` (`node_count`, `connection_count`, `density`, `avg_degree`), `node_metrics` (`in_degree`, `out_degree`, `betweenness`, `is_bottleneck` per node) and `bottlenecks` (node ids) |
| Insights | `POST /api/v1/recommendations` `{analysis_run_id}` (201, one per bottleneck of a Completed run, idempotent), `GET /api/v1/recommendations[?analysis_run_id=&status=delivered\|approved\|rejected]`, `GET /{id}`, `POST /{id}/respond` `{response: approve\|reject, comment ≤ 1000 chars}` (responder = token `sub`, answered once: Delivered → Approved \| Rejected) |

## Events (Kafka)

`ona.graph.version-published` (Graph → Analysis) and `ona.analysis.completed` (Analysis → Insights). At-least-once, consumers are idempotent. Disabled when `Kafka:BootstrapServers` is empty. See [ADR-007](docs/architecture/adr/ADR-007-kafka-events-and-jwt.md).

Defaults chosen where the product decision is still open (see [`checkpoint.md`](docs/architecture/checkpoint.md)):
- **Bottleneck:** betweenness > 0 and ≥ mean + 1 standard deviation. Administrative activity is not excluded.
- **Insights text:** rule-based; an LLM can replace it behind `IInsightGeneratorPort`.
- **Approval:** recorded only; applying a Graph update is not implemented.
- **Secrets:** the dev JWT key and admin password are in committed config for local use only. Set `JWT_KEY` and `ADMIN_PASSWORD` for anything else.

## Errors

Problem Details (`application/problem+json`) with a `code` field:

| Status | When |
| --- | --- |
| `400` | Invalid request, missing file, unreadable CSV |
| `401` / `403` | Missing or invalid token (empty body) / role not allowed; Identity also: wrong credentials (problem with `user.invalid_credentials`) |
| `409` | Identity: email already registered |
| `404` | Board / node / connection / run / recommendation / user not found |
| `422` | Business rule violated (e.g. `board.deletion_not_allowed`, `board_import.invalid`) |
| `502` | Analysis or Insights could not reach the service it depends on |
| `500` | Unexpected error |
