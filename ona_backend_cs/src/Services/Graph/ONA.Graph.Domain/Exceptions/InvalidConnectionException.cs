namespace ONA.Graph.Domain.Exceptions;

public sealed class InvalidConnectionException : DomainException
{
    public string FieldName { get; }
    public string? AttemptedValue { get; }

    public InvalidConnectionException(string fieldName, string? attemptedValue)
        : base(
            code: $"connection.invalid_{ToCode(fieldName)}",
            message: $"Connection {ToLabel(fieldName)} cannot be empty or whitespace.")
    {
        FieldName = fieldName;
        AttemptedValue = attemptedValue;
    }

    private static string ToCode(string fieldName) =>
        fieldName switch
        {
            "id" => "id",
            "board_id" => "board_id",
            "source_node_id" => "source_node_id",
            "target_node_id" => "target_node_id",
            _ => "value"
        };

    private static string ToLabel(string fieldName) =>
        fieldName switch
        {
            "board_id" => "board ID",
            "id" => "ID",
            "source_node_id" => "source node ID",
            "target_node_id" => "target node ID",
            _ => "required value"
        };
}
