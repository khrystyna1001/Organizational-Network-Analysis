namespace ONA.Graph.Application.DTOs;

public sealed record ImportBoardDto(
    string? Name,
    IReadOnlyList<ImportedEmployeeDto> Employees);

public sealed record ImportedEmployeeDto(
    string? EmployeeId,
    string? FirstName,
    string? LastName,
    string? Title,
    string? Department,
    string? SupervisorId);

public sealed record ImportedBoardDto(
    BoardDto Board,
    int NodesCreated,
    int ConnectionsCreated);
