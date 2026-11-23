using ONA.Analysis.Application.Abstractions;
using ONA.Analysis.Application.Events;
using ONA.Analysis.Application.Ports;
using ONA.Analysis.Domain.Aggregates;

namespace ONA.Analysis.Application.Tests;

internal sealed class InMemoryAnalysisRunRepository : IAnalysisRunRepository
{
    public List<AnalysisRun> Runs { get; } = [];

    public Task AddAsync(AnalysisRun run, CancellationToken cancellationToken = default)
    {
        Runs.Add(run);
        return Task.CompletedTask;
    }

    public Task<AnalysisRun?> FindCompletedByGraphVersionAsync(string graphVersionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.FirstOrDefault(r => r.GraphVersionId == graphVersionId && r.Status == AnalysisStatus.Completed));

    public Task<AnalysisRun?> GetByIdReadOnlyAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs.FirstOrDefault(r => r.Id == id));

    public Task<IReadOnlyList<AnalysisRun>> GetAllReadOnlyAsync(string? boardId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AnalysisRun>>(
            Runs.Where(r => boardId is null || r.BoardId == boardId).ToList());
}

internal sealed class FakeGraphSnapshotPort : IGraphSnapshotPort
{
    public Func<string, GraphSnapshot> Handler { get; set; } = id => throw new InvalidOperationException("not set");

    public Task<GraphSnapshot> GetSnapshotAsync(string boardId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Handler(boardId));
}

internal sealed class FakeEventPublisher : IAnalysisEventPublisher
{
    public List<AnalysisCompleted> Published { get; } = [];
    public bool ThrowOnPublish { get; set; }

    public Task PublishAnalysisCompletedAsync(AnalysisCompleted @event, CancellationToken cancellationToken = default)
    {
        if (ThrowOnPublish)
        {
            throw new InvalidOperationException("broker down");
        }

        Published.Add(@event);
        return Task.CompletedTask;
    }
}
