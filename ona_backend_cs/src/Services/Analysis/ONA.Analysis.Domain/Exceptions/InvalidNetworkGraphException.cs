namespace ONA.Analysis.Domain.Exceptions;

public sealed class InvalidNetworkGraphException : DomainException
{
    public InvalidNetworkGraphException(string reason)
        : base("analysis_run.invalid_graph", $"The graph cannot be analyzed: {reason}")
    {
    }
}
