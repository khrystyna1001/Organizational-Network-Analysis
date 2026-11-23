# ONA checkpoint

Short status for future agents. Rules: [`AGENTS.md`](../../AGENTS.md). Run/API: [`README.md`](../../README.md).

## Status

| Context | State |
| --- | --- |
| Graph | Done: boards, nodes, connections, lifecycle (Draft → Built → Published → Archived), employee CSV import (ACL), snapshot endpoint. |
| Analysis | Done: `AnalysisRun`, betweenness/degree/density metrics, bottleneck policy, HTTP adapter to Graph. |
| Insights | Done: `Recommendation` per bottleneck, approve/reject, rule-based text generator, HTTP adapter to Analysis. |
| Identity | Done: register, login (JWT), roles User/Admin, password reset by email, account edit, seeded admin. |
| Frontend | `frontend/` (plain HTML/CSS/JS served by the nginx proxy): sign in / register / reset password, import, lifecycle, interactive graph, inspector, recommendations, roster, account, admin users. |

## Decisions taken (change them in the docs first)

- Reads over HTTP ([ADR-006](adr/ADR-006-services-over-http-no-broker.md)); two Kafka events and JWT on every API ([ADR-007](adr/ADR-007-kafka-events-and-jwt.md)).
- Bottleneck = betweenness > 0 and ≥ mean + 1 standard deviation; administrative activity is **not** excluded.
- Insights text is rule-based; LLM provider not chosen.
- Approval is recorded only; no Graph update follows.
- Identity is local (no OIDC provider); `UserId` is our own id.
- New services create their database with `EnsureCreated` (no migrations); Graph uses migrations.

## Not done

- `UpdatePending`, `ApplyGraphUpdate`, `OrganizationImport` and `GraphVersion` as separate aggregates (Graph keeps the version id on `Board`).
- Communication-log import and its privacy policy. The CSV import takes a *people table* (one row per person); connection lists / edge lists (source, target, weight) are not supported yet.
- Production-grade auth: shared symmetric JWT key, no refresh tokens, no outbox for events.
- Receipts/invoices by email (a shop requirement from the course text; not part of ONA).
- LLM adapter and misinterpretation checks.
- `archive/employee_data.csv` imports (supervisors by name; the 2,945 not in the file become `External` placeholders), but it is almost a forest of 2-person pairs, so its analysis is not very meaningful. A mistyped `EmpID` also creates a placeholder instead of an error (accepted trade-off, see `context-map.md` row 14).
- MediatR package references in some `.csproj` files are unused leftovers.

## Event Storming hot spots (still open)

- How to anonymize communication content while keeping flow metadata.
- Real-time or batch graph updates.
- High score caused by administrative work vs. a real bottleneck.
- Detecting an LLM misreading the metrics.
- Exact disagreement / approval / rollback semantics.
