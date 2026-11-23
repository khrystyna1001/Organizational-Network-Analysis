namespace ONA.Analysis.Domain.Exceptions;

public sealed class InvalidIdentifierException : DomainException
{
    public InvalidIdentifierException(string identifierName, string? value)
        : base("analysis_run.invalid_identifier", $"{identifierName} must not be empty (got '{value}').")
    {
    }
}
