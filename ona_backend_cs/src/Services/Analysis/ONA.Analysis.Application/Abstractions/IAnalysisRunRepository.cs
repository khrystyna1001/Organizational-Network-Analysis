using ONA.Analysis.Domain.Aggregates;

namespace ONA.Analysis.Application.Abstractions;

public interface IAnalysisRunRepository
{
    Task AddAsync(AnalysisRun run, CancellationToken cancellationToken = default);

    /// <summary>Read-only load (no change tracking).</summary>
    Task<AnalysisRun?> GetByIdReadOnlyAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Idempotency lookup: a Completed run already bound to this graph version, if any.</summary>
    Task<AnalysisRun?> FindCompletedByGraphVersionAsync(string graphVersionId, CancellationToken cancellationToken = default);

    /// <summary>Read-only list, newest first, optionally filtered by board.</summary>
    Task<IReadOnlyList<AnalysisRun>> GetAllReadOnlyAsync(string? boardId, CancellationToken cancellationToken = default);
}
