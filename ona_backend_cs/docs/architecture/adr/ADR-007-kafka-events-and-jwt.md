# ADR-007: Kafka events between services, JWT on every API

* **Status:** Accepted
* **Date:** 2026-11-23
* **Amends:** [ADR-006](ADR-006-services-over-http-no-broker.md) (HTTP stays for reads; a broker is now used for events)

## Context
Publishing a board should start the analysis, and a finished analysis should produce recommendations, without a user clicking each step. All APIs must also require a signed-in user.

## Decision
**Kafka** (Confluent.Kafka, single broker in compose) carries two events, JSON `snake_case`, key = `board_id`:

| Topic | Producer | Consumer (group) | Payload |
| --- | --- | --- | --- |
| `ona.graph.version-published` | Graph, after `PublishBoard` is saved | Analysis (`ona-analysis`) | `event_id, occurred_at, board_id, graph_version_id` |
| `ona.analysis.completed` | Analysis, after a run is saved | Insights (`ona-insights`) | `event_id, occurred_at, analysis_run_id, board_id, graph_version_id` |

- Delivery is at-least-once (publish after commit, no outbox). Consumers are idempotent: Analysis by `GraphVersionId`, Insights by run + node.
- A failed publish never fails the HTTP request. A technical failure in a consumer is retried (offset not committed); a business failure is logged and skipped.
- Without `Kafka:BootstrapServers` the publisher is a no-op and no consumer runs (unit tests, local runs).
- The data itself is still read over HTTP (snapshot, analysis run) as in ADR-006.

**JWT** (HS256), issued by Identity at login, validated by every service with the shared `Jwt:Key`. Claims `sub, email, name, role`. Roles: `User`, `Admin`, `Service`. Background consumers call upstream APIs with a short-lived `Service` token minted from the same key; HTTP calls forward the caller's token.

## Consequences
- The whole flow works from one click (publish), and also still works by calling each API.
- One shared secret: rotate it with `Jwt__Key` on all services. Not suitable for production as is (use an asymmetric key or an identity provider).
- No outbox: a crash between the database commit and the publish loses that event; a user can still trigger the step over HTTP.
