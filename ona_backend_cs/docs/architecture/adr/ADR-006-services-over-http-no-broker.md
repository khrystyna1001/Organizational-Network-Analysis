# ADR-006: Services talk over HTTP, no message broker

* **Status:** Accepted (amended by [ADR-007](ADR-007-kafka-events-and-jwt.md))
* **Date:** 2026-11-23

## Context
Graph, Analysis, Insights and Identity are separate services. Analysis needs a graph version; Insights needs an analysis run.

## Decision
- Synchronous HTTP, one adapter per upstream in `Infrastructure/Acl`, hidden behind an Application port.
- Graph publishes `GET /api/v1/boards/{id}/snapshot` (nodes, connections, `graph_version_id`). Analysis stores the `GraphVersionId` in every run.
- Analysis publishes `GET /api/v1/analysis-runs/{id}`. Insights stores the run, version and board ids in every recommendation.
- No Kafka/MediatR (the compose file still lists Kafka; it is unused).

## Consequences
- Simple to run and test with fakes. A run needs Graph to be up when it starts.
- If decoupling is needed later, replace an adapter with a broker consumer; ports and domain stay the same.
