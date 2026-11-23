namespace ONA.Analysis.Domain.Exceptions;

public sealed class GraphNotPublishedException : DomainException
{
    public string BoardId { get; }

    public GraphNotPublishedException(string boardId)
        : base("analysis_run.graph_not_published", $"Board '{boardId}' has no published graph version to analyze.")
    {
        BoardId = boardId;
    }
}
