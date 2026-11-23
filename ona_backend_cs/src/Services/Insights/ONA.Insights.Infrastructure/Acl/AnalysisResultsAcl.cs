using ONA.Insights.Application.Ports;
using ONA.Insights.Domain.ValueObjects;

namespace ONA.Insights.Infrastructure.Acl;

/// <summary>Facade implementing <see cref="IAnalysisResultsPort"/>: adapter (I/O) + translator (mapping).</summary>
internal sealed class AnalysisResultsAcl(HttpAnalysisAdapter adapter, AnalysisRunTranslator translator)
    : IAnalysisResultsPort
{
    public async Task<AnalysisRunResult?> GetRunAsync(
        AnalysisRunId analysisRunId,
        CancellationToken cancellationToken = default)
    {
        var dto = await adapter.ReadRunAsync(analysisRunId.Value, cancellationToken);
        return dto is null ? null : translator.ToResult(dto);
    }
}
