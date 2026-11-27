# ADR-008: MediatR everywhere, live updates over SignalR

* **Status:** Accepted
* **Date:** 2026-11-27
* **Amends:** [ADR-006](ADR-006-services-over-http-no-broker.md) (MediatR is used), [ADR-007](ADR-007-kafka-events-and-jwt.md) (consumers use `ISender`; the UI no longer polls)

## Context
Docs and code disagreed: AGENTS.md promised MediatR but only some Graph handlers used it, other services had unused packages, and two SignalR hubs existed without being wired up. After publishing, the user had to press Refresh because the chain publish → Kafka → analysis → recommendations finishes asynchronously.

## Decision
**MediatR 12.4.0 in all four services** (v13+ needs a commercial licence; pinned deliberately).
- Every use case is an `IRequest` with a handler in Application. Graph nodes and connections are now handlers that delegate to `IGraphService`; controllers call `ISender`. Kafka consumers (`GraphVersionPublishedProcessor`, `AnalysisCompletedMessageHandler`) dispatch through `ISender`.
- Each service registers `RequestLoggingBehavior<,>` (Infrastructure, `internal sealed`) as an open pipeline behavior. It logs only the request type name and duration, never request content.

**SignalR for live UI updates** (hubs `/hubs/analysis` and `/hubs/insights`, JSON `snake_case`).
- Application raises MediatR `INotification`s through `IPublisher`: `AnalysisRunCompletedNotification` (Analysis), `RecommendationsGeneratedNotification` and `RecommendationRespondedNotification` (Insights). Presentation handlers forward them with `IHubContext<T>`. Application therefore does not depend on `Microsoft.AspNetCore` (architecture tests stay valid).
- Clients join groups `board:{id}` (and `run:{id}` on Insights). Messages: `analysis-run-completed`, `recommendations-generated`, `recommendation-responded`.
- Notifications are best effort: a failing handler is logged and never fails the request or the Kafka message.
- Browsers cannot set headers on WebSockets, so the JWT is sent as the `access_token` query parameter and read in `JwtBearerEvents.OnMessageReceived` for `/hubs` paths only. nginx therefore logs the token in access logs for those two routes; accepted for this course project, to be replaced by a cookie or ticket for production. Hubs are `[Authorize]`.
- nginx proxies the hubs with WebSocket Upgrade headers. The frontend uses the vendored `signalr.min.js` (MIT), shows a live/connecting/offline pill, re-joins its board after a reconnect and re-reads over HTTP; if SignalR is unavailable it falls back to polling.

## Consequences
- One way to run a use case; the docs now match the code. Behaviors give uniform logging with no per-handler code.
- No Refresh button needed while live. Missed messages are not replayed; state always comes from HTTP.
- Several replicas of a service would need a SignalR backplane (not done).
- Staying on 12.4.0 means no MediatR upgrades without a licence decision.
