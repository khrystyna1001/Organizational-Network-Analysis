# Copilot instructions – ONA

Follow [`AGENTS.md`](../AGENTS.md). Essentials:

- Specs in `docs/architecture/*.md` are the source of truth for names.
- Plan → Red → Green; done = `dotnet test` with `Failed: 0`.
- Domain: no packages, no public setters, `sealed` `DomainException`s with `Code`.
- CQRS: tracked writes via aggregate methods, `AsNoTracking` reads returning DTOs.
- Infrastructure and ACL classes are `internal sealed` (except `DependencyInjection` and migrations); foreign `*_DTO` types never leave `Acl/`.
