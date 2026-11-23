namespace ONA.Insights.Domain.Exceptions;

public sealed class NoBottlenecksException : DomainException
{
    public NoBottlenecksException(string analysisRunId)
        : base("recommendation.no_bottlenecks", $"Analysis run '{analysisRunId}' found no bottlenecks, nothing to recommend.")
    {
    }
}
