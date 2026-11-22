# Lab 5 – summary

- **Stage 1:** `AGENTS.md`, `.github/copilot-instructions.md`, `docs/architecture/*.md`
- **Stage 2:** `tests/Graph/ONA.Graph.ArchitectureTests` (8 rules)
- **Stage 3:** `Application/Features/Boards` + `BoardsController`, migration `AddBoardLifecycle`
- **Stage 4:** `Application/Ports/IEmployeeDirectoryPort.cs`, `Infrastructure/Acl`, `tests/Graph/ONA.Graph.Infrastructure.Tests`

## Run
```bash
dotnet test ONA.sln --filter "FullyQualifiedName!~IntegrationTests"
dotnet run --project src/Services/Graph/ONA.Graph.Presentation   # then ONA.Graph.Presentation.http
```

## Mutation demo (each turns tests red)
1. `Board.cs`: `Lifecycle { get; private set; }` → `{ get; set; }`
2. `HrisDtos.cs`: `internal sealed class HRIS_EMPLOYEE_DTO` → `public`
3. `HrisModelTranslator.cs`: comment out the unknown-supervisor `throw`
4. `Board.EnsureCanBeDeleted`: comment out the `throw`
