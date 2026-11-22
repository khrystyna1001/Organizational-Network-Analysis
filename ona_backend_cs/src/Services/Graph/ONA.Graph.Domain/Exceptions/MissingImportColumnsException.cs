namespace ONA.Graph.Domain.Exceptions;

public sealed class MissingImportColumnsException : DomainException
{
    public IReadOnlyList<string> MissingColumns { get; }

    public MissingImportColumnsException(IReadOnlyList<string> missingColumns)
        : base(
            "board_import.missing_columns",
            $"Organization import is missing required columns: {string.Join(", ", missingColumns)}.")
    {
        MissingColumns = missingColumns;
    }
}
