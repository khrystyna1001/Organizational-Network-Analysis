# ADR-002: Host Organization and Graph in one service

* **Status:** Superseded by [ADR-003](./ADR-003-single-graph-service-with-csv-import.md)
* **Date:** 2026-11-10
* **Project context:** ONA — Organization and Graph bounded contexts

> This ADR records the earlier shared-host decision. It was superseded when the user requested one Graph service with the CSV import endpoint implemented directly in Graph Presentation; see ADR-003.

## 1. Context and quality scenario

ONA originally ran the Organization CSV API and Graph API as separate ASP.NET Core hosts. The user selected combining them into one service. The target must still preserve the distinct responsibilities and domain models: Organization accepts and validates source data; Graph owns boards, nodes, connections, and graph consistency.

**Quality scenario — deploy the Organization and Graph APIs together without changing their contracts:**

* **Source and stimulus:** An operator deploys the Organization + Graph service and clients call either existing API.
* **Artifact:** The shared HTTP host and the Organization and Graph bounded contexts.
* **Environment:** Normal operation through the combined host.
* **Expected response:** One deployable host serves both APIs, while each context retains ownership of its domain rules and application workflows.
* **Response measure:** Existing Organization and Graph routes, request/response shapes, and domain error behavior work through the shared host. A numeric availability or latency target has not been specified.

At the time, this decision was implemented temporarily by `ONA.OrganizationGraph.Presentation`, which composed both Presentation assemblies and their infrastructure registrations on HTTP `5056` / HTTPS `7089`. That temporary host and the separate Organization service were subsequently removed; the current single-Graph-service decision is recorded in ADR-003.

## 2. Considered options

1. **Keep separate Organization and Graph hosts**
   * **Advantages:** Existing deployment boundaries remain unchanged; each API can be started and released independently.
   * **Disadvantages:** Does not meet the selected requirement to combine them into one service and retains two runtime hosts for closely related import-to-graph workflows.

2. **Use one deployable host while preserving separate bounded contexts**
   * **Advantages:** Meets the single-host target while preserving independent domain ownership, application contracts, and the ability to keep context internals separate.
   * **Disadvantages:** The host becomes a shared composition and deployment boundary; routing, dependency registration, OpenAPI descriptions, configuration, and release cadence must be coordinated.

3. **Merge Organization and Graph into one bounded context and domain model**
   * **Advantages:** Direct in-process calls and a simpler apparent project structure.
   * **Disadvantages:** Conflates import processing with graph consistency, expands the domain boundary, and creates unnecessary coupling between business capabilities.

## 3. Decision

We choose **Option 2**. Organization and Graph will be served by one deployable ASP.NET Core host, but remain separate bounded contexts. Their Domain, Application, and Infrastructure layers retain separate ownership; neither context's core projects may reference the other's projects or domain types. A dedicated Presentation/composition root may wire both contexts together and expose their APIs.

Preserve the current public routes and behavior as the compatibility baseline:

* Organization CSV upload: `POST /api/v1/data`
* Graph health: `GET /`
* Graph boards: `/api/v1/boards`
* Graph nodes: `/api/v1/nodes`
* Graph connections: `/api/v1/connections`

Keep the contexts' persistence ownership separate, including their own database/schema boundaries if and when MySQL persistence is implemented. The combined OpenAPI document includes the routes from both contexts. The separate standalone Presentation projects remain as compatibility entry points.

## 4. Consequences

### Benefits (+)

* Organization and Graph are deployed as one service as requested, with one runtime host to operate.
* Import logic remains owned by Organization, while Board and graph invariants remain owned by Graph.
* Existing HTTP contracts can be preserved, avoiding an unrelated client migration.
* Separate context projects and persistence ownership retain architectural boundaries and make future extraction possible.

### Costs and risks (-)

* The shared host coordinates startup, configuration, health, logging, controller registration, and OpenAPI for both contexts.
* A release or host failure affects both APIs; independent deployment and scaling are no longer available at the host boundary.
* Explicit in-process interfaces or versioned contracts are still needed between contexts; the shared host is not permission to share domain entities or bypass aggregate roots.
* Operators must use the combined host's launch profile for the standard runtime; launching the compatibility hosts independently can still produce two processes and duplicate APIs.
* Shared deployment and scaling mean a host release or process failure affects both APIs. No database or event-broker integration was introduced as part of this decision.
