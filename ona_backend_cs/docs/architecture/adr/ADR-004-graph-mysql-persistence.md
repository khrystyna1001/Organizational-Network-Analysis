# ADR-004: Persist Graph aggregates in MySQL

> **Update:** migrations are now applied automatically at startup (`InitializeGraphDatabaseAsync`); `dotnet ef database update` is optional.

* **Status:** Accepted
* **Date:** 2026-11-10
* **Project context:** ONA Graph service

## 1. Context and quality scenario

Graph boards, nodes, and connections were stored in a process-local dictionary and were lost whenever the Graph host restarted. The user selected MySQL, with local development using Docker, and scoped this persistence change to Graph CRUD. CSV parsing remains in-memory and returns parsed rows; it does not persist import batches. Identity, Analysis, and Insights remain scaffolds without configured databases.

**Quality scenario — preserve Graph state across process restarts:**

* **Source and stimulus:** A Graph API client creates or updates a board, its nodes, and connections.
* **Artifact:** Graph `Board` aggregate and its child entities.
* **Environment:** Normal operation against MySQL.
* **Expected response:** Accepted mutations are stored durably and can be retrieved by a later request/process.
* **Response measure:** MySQL integration tests apply the committed schema migrations and verify graph data, relationships, and node-deletion cleanup across independent DbContext scopes.

## 2. Considered options

1. **Keep the process-local in-memory repository**
   * **Advantage:** No database setup and fast isolated development.
   * **Disadvantage:** State is lost on restart and cannot support durable Graph workflows.

2. **Use a Graph-owned EF Core context and MySQL repository**
   * **Advantage:** Persists only Graph-owned data, keeps Domain and Application independent of the database provider, and uses migrations to evolve the schema.
   * **Disadvantage:** Requires an available MySQL instance, explicit migration application, and database integration tests.

3. **Introduce shared persistence across all contexts**
   * **Advantage:** Could simplify an early deployment's database provisioning.
   * **Disadvantage:** No other context has implemented workflows that need persistence; a shared persistence model would blur ownership and add unused infrastructure.

## 3. Decision

We choose **Option 2**. `ONA.Graph.Infrastructure` owns `GraphDbContext`, MySQL mappings, migrations, and `MySqlBoardRepository`. The API reads the `Graph` connection string from `ConnectionStrings:Graph`; local development can provide it using ASP.NET User Secrets, while deployment provides `ConnectionStrings__Graph` from the hosting platform's secret configuration. No credentials are committed.

Use Oracle's `MySql.EntityFrameworkCore` 10.0.12 with EF Core 10.0.14. The provider package reports support for EF Core 8, 9, and 10; Pomelo's latest stable version at selection time was 9.0.0, so Oracle's provider was selected for the .NET 10 / EF Core 10 target. Local integration tests use MySQL 8.4 through Testcontainers.

Persist boards, nodes, and connections only. The schema enforces board ownership and node references using foreign keys, cascades cleanup on deletion, and prevents duplicate directed connections with a unique index. Keep aggregate behavior and invariants in Graph Domain. CSV imports continue to parse in-memory; no import rows or payloads are persisted.

Apply migrations explicitly with `dotnet ef database update` before starting the API. Do not automatically run migrations at web-host startup; migration execution is an operational/deployment step. Contexts without implemented business workflows do not receive placeholder DbContexts.

## 4. Consequences

### Benefits (+)

* Graph CRUD data survives application restarts.
* Database ownership is isolated to Graph Infrastructure; Domain and Application do not depend on EF Core or MySQL.
* Database constraints reinforce aggregate relationships and directed-connection uniqueness.
* Integration tests exercise actual MySQL behavior and the committed migrations.

### Costs and risks (-)

* Graph host startup requires a valid connection string and a database whose migrations have been applied.
* Operators must run migrations as an explicit deployment step and coordinate schema changes with application releases.
* Test execution requires Docker to run the MySQL Testcontainer.
* Raw import batches and all non-Graph workflows remain unpersisted; employee nodes and supervisor connections created from CSV are persisted as Graph data.
* Production hosting, backups, TLS/network policy, and operational database configuration remain to be selected before production deployment.
