using EchoAPI.Graph.Domain.Exceptions;

namespace EchoAPI.Graph.Domain.Entities;

public sealed class Node
{
    public string Id { get; }
    public string BoardId { get; }
    public string Name { get; private set; }
    public string Title { get; private set; }
    public string Department { get; private set; }

    internal Node(
        string? id,
        string? boardId,
        string? name,
        string? title,
        string? department)
    {
        Id = Require(id, "id");
        BoardId = Require(boardId, "board_id");
        Name = Require(name, "name");
        Title = Require(title, "title");
        Department = Require(department, "department");
    }

    internal void Update(string? name, string? title, string? department)
    {
        var updatedName = Require(name, "name");
        var updatedTitle = Require(title, "title");
        var updatedDepartment = Require(department, "department");

        Name = updatedName;
        Title = updatedTitle;
        Department = updatedDepartment;
    }

    private static string Require(string? value, string fieldName) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidNodeException(fieldName, value);
}
