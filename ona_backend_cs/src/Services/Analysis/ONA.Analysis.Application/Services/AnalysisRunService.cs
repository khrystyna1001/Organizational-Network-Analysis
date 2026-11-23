using ONA.Analysis.Application.Abstractions;
using ONA.Analysis.Application.Events;
using ONA.Analysis.Application.Ports;
using ONA.Analysis.Domain.Aggregates;
using ONA.Analysis.Domain.Entities;
using ONA.Analysis.Domain.Services;
using ONA.Analysis.Domain.ValueObjects;

namespace ONA.Analysis.Application.Services;

/// <summary>
/// Shared use-case logic for "run an analysis on the published graph of a board":
/// read snapshot -> calculate -> save -> publish <see cref="AnalysisCompleted"/>.
/// Used by the HTTP command and by the Kafka-triggered command.
/// </summary>
public sealed class AnalysisRunService(
    IAnalysisRunRepository repository,
    IGraphSnapshotPort graphSnapshotPort,
    IAnalysisEventPublisher eventPublisher,
    TimeProvider timeProvider)
{
    private readonly NetworkMetricsCalculator _calculator = new();

    /// <summary>Always creates a new run (explicit user request).</summary>
    public async Task<AnalysisRun> StartAsync(string boardId, CancellationToken cancellationToken = default)
    {
        var id = new BoardId(boardId);
        var snapshot = await graphSnapshotPort.GetSnapshotAsync(id.Value, cancellationToken);
        return await CalculateSaveAndPublishAsync(snapshot, cancellationToken);
    }

    /// <summary>
    /// Idempotent by GraphVersionId: returns null (and does nothing) when a Completed run for the
    /// version already exists – safe for at-least-once event delivery.
    /// </summary>
    public async Task<AnalysisRun?> StartForGraphVersionAsync(
        string boardId,
        string graphVersionId,
        CancellationToken cancellationToken = default)
    {
        var id = new BoardId(boardId);
        var version = new GraphVersionId(graphVersionId);
        if (await repository.FindCompletedByGraphVersionAsync(version.Value, cancellationToken) is not null)
        {
            return null;
        }

        var snapshot = await graphSnapshotPort.GetSnapshotAsync(id.Value, cancellationToken);

        // The board may already have a newer version than the event announced: analyse what is current,
        // but still only once per version.
        if (snapshot.GraphVersionId != version.Value &&
            await repository.FindCompletedByGraphVersionAsync(snapshot.GraphVersionId, cancellationToken) is not null)
        {
            return null;
        }

        return await CalculateSaveAndPublishAsync(snapshot, cancellationToken);
    }

    private async Task<AnalysisRun> CalculateSaveAndPublishAsync(
        GraphSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var graph = new NetworkGraph(
            snapshot.Nodes.Select(n => new NetworkNode(n.Id, n.Name)),
            snapshot.Connections.Select(c => new NetworkConnection(c.SourceNodeId, c.TargetNodeId, c.Weight)));

        var run = new AnalysisRun(AnalysisRunId.New().Value, snapshot.BoardId, snapshot.GraphVersionId);
        run.Complete(_calculator.Calculate(graph));

        await repository.AddAsync(run, cancellationToken);
        await PublishCompletedAsync(run, cancellationToken);
        return run;
    }

    private async Task PublishCompletedAsync(AnalysisRun run, CancellationToken cancellationToken)
    {
        try
        {
            await eventPublisher.PublishAnalysisCompletedAsync(
                new AnalysisCompleted(
                    Guid.NewGuid().ToString(),
                    timeProvider.GetUtcNow().UtcDateTime,
                    run.Id,
                    run.BoardId,
                    run.GraphVersionId),
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The run is already saved; the transport adapter has logged the failure.
            // A lost event must never turn a successful analysis into an error.
        }
    }
}
