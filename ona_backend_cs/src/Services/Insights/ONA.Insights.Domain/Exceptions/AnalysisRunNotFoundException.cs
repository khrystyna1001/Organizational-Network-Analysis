namespace ONA.Insights.Domain.Exceptions;

public sealed class AnalysisRunNotFoundException : DomainException
{
    public AnalysisRunNotFoundException(string analysisRunId)
        : base("recommendation.analysis_run_not_found", $"Analysis run '{analysisRunId}' was not found.")
    {
    }
}
