using ONA.Analysis.Application.Abstractions;
using ONA.Analysis.Application.Abstractions.Messaging;
using ONA.Analysis.Application.DTOs;

namespace ONA.Analysis.Application.Features.AnalysisRuns.Queries;

/// <summary>READ (single): no change tracking, returns a DTO or null.</summary>
public sealed record GetAnalysisRunByIdQuery(string AnalysisRunId) : IQuery<AnalysisRunDto?>;

public sealed class GetAnalysisRunByIdQueryHandler(IAnalysisRunRepository repository)
    : IQueryHandler<GetAnalysisRunByIdQuery, AnalysisRunDto?>
{
    public async Task<AnalysisRunDto?> HandleAsync(
        GetAnalysisRunByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var run = await repository.GetByIdReadOnlyAsync(query.AnalysisRunId, cancellationToken);
        return run is null ? null : AnalysisRunDto.FromDomain(run);
    }
}

/// <summary>READ (list): optionally filtered by board, newest first.</summary>
public sealed record GetAnalysisRunsQuery(string? BoardId) : IQuery<IReadOnlyList<AnalysisRunDto>>;

public sealed class GetAnalysisRunsQueryHandler(IAnalysisRunRepository repository)
    : IQueryHandler<GetAnalysisRunsQuery, IReadOnlyList<AnalysisRunDto>>
{
    public async Task<IReadOnlyList<AnalysisRunDto>> HandleAsync(
        GetAnalysisRunsQuery query,
        CancellationToken cancellationToken = default)
    {
        var runs = await repository.GetAllReadOnlyAsync(query.BoardId, cancellationToken);
        return runs.Select(AnalysisRunDto.FromDomain).ToArray();
    }
}
