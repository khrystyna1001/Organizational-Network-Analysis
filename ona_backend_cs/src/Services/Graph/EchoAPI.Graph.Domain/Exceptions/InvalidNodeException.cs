namespace EchoAPI.Graph.Domain.Exceptions;

public sealed class InvalidNodeException : DomainException
{
    public string FieldName { get; }
    public string? AttemptedValue { get; }

    public InvalidNodeException(string fieldName, string? attemptedValue)
        : base(
            code: $"node.invalid_{ToCode(fieldName)}",
            message: $"Node {ToLabel(fieldName)} cannot be empty or whitespace.")
    {
        FieldName = fieldName;
        AttemptedValue = attemptedValue;
    }

    private static string ToCode(string fieldName) =>
        fieldName switch
        {
            "board_id" => "board_id",
            "id" => "id",
            "name" => "name",
            "title" => "title",
            "department" => "department",
            _ => "value"
        };

    private static string ToLabel(string fieldName) =>
        fieldName switch
        {
            "board_id" => "board ID",
            "id" => "ID",
            "name" => "name",
            "title" => "title",
            "department" => "department",
            _ => "required value"
        };
}
