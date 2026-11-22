namespace ONA.Graph.Application.DTOs;

/// <summary>One employee accepted by the import, expressed in our model (not the HRIS format).</summary>
public sealed record ImportedMemberDto(
    string EmployeeId,
    string Name,
    string Title,
    string Department,
    string? SupervisorEmployeeId);

public sealed record ImportedBoardDto(
    BoardDto Board,
    int NodesCreated,
    int ConnectionsCreated,
    IReadOnlyList<ImportedMemberDto> Members);
