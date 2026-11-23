namespace ONA.Graph.Domain.Exceptions;

/// <summary>The CSV column mapping itself is unusable (for example no column gives a person's name).</summary>
public sealed class InvalidColumnMappingException(string reason)
    : DomainException("board_import.invalid_mapping", reason);
