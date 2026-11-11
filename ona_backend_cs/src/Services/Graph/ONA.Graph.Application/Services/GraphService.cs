using ONA.Graph.Application.Abstractions;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Domain.Aggregates;
using ONA.Graph.Domain.Entities;
using ONA.Graph.Domain.Exceptions;

namespace ONA.Graph.Application.Services;

public sealed class GraphService(IBoardRepository repository) : IGraphService
{
    public async Task<BoardDto> CreateBoardAsync(
        CreateBoardDto input,
        CancellationToken cancellationToken = default)
    {
        var board = new Board(Guid.NewGuid().ToString(), input.Name, input.Description);
        await repository.SaveAsync(board, cancellationToken);
        return ToDto(board);
    }

    public async Task<ImportedBoardDto> CreateBoardFromImportAsync(
        ImportBoardDto input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Employees);

        if (input.Employees.Count == 0)
        {
            throw new InvalidBoardImportException("CSV must contain at least one employee row.");
        }

        var organizationNetwork = new OrganizationNetwork(input.Employees);
        organizationNetwork.CreateGraphFromEmployees();

        var employeeIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < input.Employees.Count; index++)
        {
            var employee = input.Employees[index];
            var rowNumber = index + 2;
            var employeeId = RequireImportValue(employee.EmployeeId, rowNumber, "EmpID");
            RequireImportValue(employee.FirstName, rowNumber, "FirstName");
            RequireImportValue(employee.LastName, rowNumber, "LastName");
            RequireImportValue(employee.Title, rowNumber, "Title");
            RequireImportValue(employee.Department, rowNumber, "DepartmentType");

            if (!employeeIds.Add(employeeId))
            {
                throw new InvalidBoardImportException(
                    rowNumber,
                    "EmpID",
                    $"employee ID '{employeeId}' is duplicated.");
            }
        }

        var employeeIdSet = input.Employees
            .Select(employee => employee.EmployeeId!.Trim())
            .ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < input.Employees.Count; index++)
        {
            var employee = input.Employees[index];
            var supervisorId = employee.SupervisorId?.Trim();
            if (string.IsNullOrEmpty(supervisorId))
            {
                continue;
            }

            if (!employeeIdSet.Contains(supervisorId))
            {
                throw new InvalidBoardImportException(
                    index + 2,
                    "Supervisor",
                    $"employee ID '{supervisorId}' does not exist in this CSV.");
            }

            if (string.Equals(
                    supervisorId,
                    employee.EmployeeId!.Trim(),
                    StringComparison.Ordinal))
            {
                throw new InvalidBoardImportException(
                    index + 2,
                    "Supervisor",
                    "an employee cannot supervise themself.");
            }
        }

        var board = new Board(
            Guid.NewGuid().ToString(),
            input.Name,
            null);
        var nodesByEmployeeId = new Dictionary<string, Node>(StringComparer.Ordinal);

        foreach (var employee in input.Employees)
        {
            var employeeId = employee.EmployeeId!.Trim();
            var node = board.AddNode(
                $"{employee.FirstName!.Trim()} {employee.LastName!.Trim()}",
                employee.Title,
                employee.Department);
            nodesByEmployeeId.Add(employeeId, node);
        }

        foreach (var employee in input.Employees)
        {
            var supervisorId = employee.SupervisorId?.Trim();
            if (string.IsNullOrEmpty(supervisorId))
            {
                continue;
            }

            board.AddConnection(
                nodesByEmployeeId[supervisorId].Id,
                nodesByEmployeeId[employee.EmployeeId!.Trim()].Id,
                1.0);
        }

        await repository.SaveAsync(board, cancellationToken);
        return new ImportedBoardDto(
            ToDto(board),
            board.Nodes.Count,
            board.Connections.Count);
    }

    public async Task<IReadOnlyList<BoardDto>> GetBoardsAsync(
        CancellationToken cancellationToken = default)
    {
        var boards = await repository.GetAllAsync(cancellationToken);
        return boards.Select(ToDto).ToArray();
    }

    public async Task<BoardDto?> GetBoardAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var board = await repository.GetByIdAsync(id, cancellationToken);
        return board is null ? null : ToDto(board);
    }

    public async Task<BoardDto?> UpdateBoardAsync(
        string id,
        UpdateBoardDto input,
        CancellationToken cancellationToken = default)
    {
        var board = await repository.GetByIdAsync(id, cancellationToken);
        if (board is null)
        {
            return null;
        }

        board.UpdateDetails(
            input.Name ?? board.Name,
            input.DescriptionSpecified ? input.Description : board.Description);
        await repository.SaveAsync(board, cancellationToken);
        return ToDto(board);
    }

    public Task<bool> DeleteBoardAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        repository.DeleteAsync(id, cancellationToken);

    public async Task<NodeDto> AddNodeAsync(
        string boardId,
        CreateNodeDto input,
        CancellationToken cancellationToken = default)
    {
        var board = await GetRequiredBoardAsync(boardId, cancellationToken);
        var node = board.AddNode(input.Name, input.Title, input.Department);
        await repository.SaveAsync(board, cancellationToken);
        return ToDto(node);
    }

    public async Task<IReadOnlyList<NodeDto>> GetNodesAsync(
        string? boardId = null,
        CancellationToken cancellationToken = default)
    {
        var boards = await repository.GetAllAsync(cancellationToken);
        return boards
            .Where(board => string.IsNullOrEmpty(boardId) || board.Id == boardId)
            .SelectMany(board => board.Nodes.Select(ToDto))
            .ToArray();
    }

    public async Task<NodeDto?> GetNodeAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var board = await FindBoardContainingNodeAsync(id, cancellationToken);
        var node = board?.FindNode(id);
        return node is null ? null : ToDto(node);
    }

    public async Task<NodeDto?> UpdateNodeAsync(
        string id,
        UpdateNodeDto input,
        CancellationToken cancellationToken = default)
    {
        var board = await FindBoardContainingNodeAsync(id, cancellationToken);
        var node = board?.FindNode(id);
        if (board is null || node is null)
        {
            return null;
        }

        if (!board.UpdateNode(
                id,
                input.Name ?? node.Name,
                input.Title ?? node.Title,
                input.Department ?? node.Department))
        {
            return null;
        }

        await repository.SaveAsync(board, cancellationToken);
        return board.FindNode(id) is { } updated ? ToDto(updated) : null;
    }

    public async Task<bool> DeleteNodeAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var board = await FindBoardContainingNodeAsync(id, cancellationToken);
        if (board is null || !board.RemoveNode(id))
        {
            return false;
        }

        await repository.SaveAsync(board, cancellationToken);
        return true;
    }

    public async Task<ConnectionDto> AddConnectionAsync(
        string boardId,
        CreateConnectionDto input,
        CancellationToken cancellationToken = default)
    {
        var board = await GetRequiredBoardAsync(boardId, cancellationToken);
        var connection = board.AddConnection(
            input.SourceNodeId,
            input.TargetNodeId,
            input.Weight);
        await repository.SaveAsync(board, cancellationToken);
        return ToDto(connection);
    }

    public async Task<IReadOnlyList<ConnectionDto>> GetConnectionsAsync(
        string? boardId = null,
        CancellationToken cancellationToken = default)
    {
        var boards = await repository.GetAllAsync(cancellationToken);
        return boards
            .Where(board => string.IsNullOrEmpty(boardId) || board.Id == boardId)
            .SelectMany(board => board.Connections.Select(ToDto))
            .ToArray();
    }

    public async Task<ConnectionDto?> GetConnectionAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var board = await FindBoardContainingConnectionAsync(id, cancellationToken);
        var connection = board?.FindConnection(id);
        return connection is null ? null : ToDto(connection);
    }

    public async Task<ConnectionDto?> UpdateConnectionAsync(
        string id,
        UpdateConnectionDto input,
        CancellationToken cancellationToken = default)
    {
        var board = await FindBoardContainingConnectionAsync(id, cancellationToken);
        var connection = board?.FindConnection(id);
        if (board is null || connection is null)
        {
            return null;
        }

        var weight = input.WeightSpecified ? input.Weight : connection.Weight;
        if (!board.UpdateConnectionWeight(id, weight))
        {
            return null;
        }

        await repository.SaveAsync(board, cancellationToken);
        return board.FindConnection(id) is { } updated ? ToDto(updated) : null;
    }

    public async Task<bool> DeleteConnectionAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var board = await FindBoardContainingConnectionAsync(id, cancellationToken);
        if (board is null || !board.RemoveConnection(id))
        {
            return false;
        }

        await repository.SaveAsync(board, cancellationToken);
        return true;
    }

    private async Task<Board> GetRequiredBoardAsync(
        string id,
        CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
        ?? throw new BoardNotFoundException(id);

    private async Task<Board?> FindBoardContainingNodeAsync(
        string nodeId,
        CancellationToken cancellationToken)
    {
        var boards = await repository.GetAllAsync(cancellationToken);
        return boards.FirstOrDefault(board => board.FindNode(nodeId) is not null);
    }

    private async Task<Board?> FindBoardContainingConnectionAsync(
        string connectionId,
        CancellationToken cancellationToken)
    {
        var boards = await repository.GetAllAsync(cancellationToken);
        return boards.FirstOrDefault(board => board.FindConnection(connectionId) is not null);
    }

    private static BoardDto ToDto(Board board) =>
        new(board.Id, board.Name, board.Description);

    private static NodeDto ToDto(Node node) =>
        new(node.Id, node.BoardId, node.Name, node.Title, node.Department);

    private static ConnectionDto ToDto(Connection connection) =>
        new(
            connection.Id,
            connection.BoardId,
            connection.SourceNodeId,
            connection.TargetNodeId,
            connection.Weight);

    private static string RequireImportValue(string? value, int rowNumber, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidBoardImportException(rowNumber, field, "value is required.");
        }

        return value.Trim();
    }
}
