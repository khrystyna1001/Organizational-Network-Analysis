using ONA.Graph.Application.Abstractions;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Ports;
using ONA.Graph.Domain.Aggregates;
using ONA.Graph.Domain.Entities;

namespace ONA.Graph.Application.Features.Boards.Commands;

/// <summary>
/// CREATE from an external HRIS employee export. The foreign format is translated by the
/// Anti-Corruption Layer behind <see cref="IEmployeeDirectoryPort"/>; this handler sees only
/// our model (NodeName, DepartmentName, supervisor references).
/// </summary>
public sealed record ImportBoardFromHrisCommand(string BoardName, Stream EmployeeExport, ColumnMapping? Mapping = null)
    : IRequest<ImportedBoardDto>;

public sealed class ImportBoardFromHrisCommandHandler(
    IEmployeeDirectoryPort employeeDirectory,
    IBoardRepository repository)
    : IRequestHandler<ImportBoardFromHrisCommand, ImportedBoardDto>
{
    /// <summary>Weight given to every supervisor -> subordinate relationship.</summary>
    private const double ReportingLineWeight = 1.0;

    public async Task<ImportedBoardDto> Handle(
        ImportBoardFromHrisCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command.EmployeeExport);

        var organization = await employeeDirectory.ReadOrganizationAsync(
            command.EmployeeExport,
            command.Mapping,
            cancellationToken);

        var board = new Board(Guid.NewGuid().ToString(), command.BoardName, null);
        var nodesByEmployeeId = new Dictionary<string, Node>(StringComparer.Ordinal);

        foreach (var member in organization.Members)
        {
            nodesByEmployeeId.Add(
                member.EmployeeId,
                board.AddNode(member.Name, member.Title, member.Department));
        }

        foreach (var member in organization.Members)
        {
            if (member.SupervisorEmployeeId is null)
            {
                continue;
            }

            board.AddConnection(
                nodesByEmployeeId[member.SupervisorEmployeeId].Id,
                nodesByEmployeeId[member.EmployeeId].Id,
                ReportingLineWeight);
        }

        await repository.AddAsync(board, cancellationToken);

        return new ImportedBoardDto(
            BoardDto.FromDomain(board),
            board.Nodes.Count,
            board.Connections.Count,
            organization.Members
                .Select(member => new ImportedMemberDto(
                    member.EmployeeId,
                    member.Name.Value,
                    member.Title,
                    member.Department.Value,
                    member.SupervisorEmployeeId))
                .ToArray());
    }
}
