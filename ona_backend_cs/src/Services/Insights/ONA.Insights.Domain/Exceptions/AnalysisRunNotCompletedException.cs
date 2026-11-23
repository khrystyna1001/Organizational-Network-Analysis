namespace ONA.Insights.Domain.Exceptions;

public sealed class AnalysisRunNotCompletedException : DomainException
{
    public AnalysisRunNotCompletedException(string analysisRunId, string status)
        : base("recommendation.analysis_run_not_completed", $"Analysis run '{analysisRunId}' is {status}, recommendations need a Completed run.")
    {
    }
}
