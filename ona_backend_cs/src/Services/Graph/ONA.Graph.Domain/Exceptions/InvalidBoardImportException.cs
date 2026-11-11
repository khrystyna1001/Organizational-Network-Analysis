namespace ONA.Graph.Domain.Exceptions;

public sealed class InvalidBoardImportException : DomainException
{
    public InvalidBoardImportException(int rowNumber, string field, string reason)
        : base(
            "board_import.invalid",
            $"CSV row {rowNumber} has an invalid '{field}' value: {reason}")
    {
    }

    public InvalidBoardImportException(string reason)
        : base("board_import.invalid", reason)
    {
    }
}
