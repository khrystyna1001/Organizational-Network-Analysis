namespace ONA.Analysis.Domain.Exceptions;

public sealed class AnalysisRunNotFoundException : DomainException
{
    public string AnalysisRunId { get; }

    public AnalysisRunNotFoundException(string analysisRunId)
        : base("analysis_run.not_found", $"Analysis run '{analysisRunId}' was not found.")
    {
        AnalysisRunId = analysisRunId;
    }
}
