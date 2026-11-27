using ONA.Insights.Application.Abstractions;
using ONA.Insights.Application.Ports;
using ONA.Insights.Domain.Aggregates;
using ONA.Insights.Domain.ValueObjects;

namespace ONA.Insights.Application.Tests;

internal sealed class InMemoryRecommendationRepository : IRecommendationRepository
{
    public List<Recommendation> Items { get; } = [];
    public int SaveCount { get; private set; }

    public Task<Recommendation?> GetByIdAsync(RecommendationId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(r => r.Id.Equals(id)));

    public Task<Recommendation?> GetByIdReadOnlyAsync(RecommendationId id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Recommendation>> ListAsync(
        AnalysisRunId? analysisRunId, RecommendationStatus? status, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Recommendation>>(Items
            .Where(r => analysisRunId is null || r.AnalysisRunId.Equals(analysisRunId))
            .Where(r => status is null || r.Status == status)
            .ToList());

    public Task AddRangeAsync(IReadOnlyCollection<Recommendation> recommendations, CancellationToken cancellationToken = default)
    {
        Items.AddRange(recommendations);
        return Task.CompletedTask;
    }

    public Task SaveAsync(Recommendation recommendation, CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeAnalysisResultsPort(AnalysisRunResult? run) : IAnalysisResultsPort
{
    public Task<AnalysisRunResult?> GetRunAsync(AnalysisRunId analysisRunId, CancellationToken cancellationToken = default) =>
        Task.FromResult(run is not null && run.Id.Equals(analysisRunId) ? run : null);
}

internal sealed class FakeInsightGenerator : IInsightGeneratorPort
{
    public int Calls { get; private set; }

    public Task<string> GenerateAsync(InsightRequest request, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult($"Advice for {request.NodeName}");
    }
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal static class Runs
{
    public static AnalysisRunResult Completed(string id = "run1", params string[] bottleneckIds)
    {
        var nodes = new[] { "n1", "n2", "n3" }
            .Select((nodeId, i) => new AnalysisNodeResult(
                new TargetNodeId(nodeId), $"Person {nodeId}", i, i + 1, i * 0.5, bottleneckIds.Contains(nodeId)))
            .ToList();
        return new AnalysisRunResult(
            new AnalysisRunId(id), new BoardId("b1"), new GraphVersionId("v1"), AnalysisRunStatus.Completed, nodes);
    }
}

/// <summary>Records MediatR notifications instead of dispatching them (no SignalR in Application tests).</summary>
internal sealed class RecordingPublisher : IPublisher
{
    public List<object> Published { get; } = [];
    public bool ThrowOnPublish { get; set; }

    public Task Publish(object notification, CancellationToken cancellationToken = default) => Record(notification);

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification => Record(notification);

    private Task Record(object notification)
    {
        if (ThrowOnPublish)
        {
            throw new InvalidOperationException("signalr down");
        }

        Published.Add(notification);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Minimal mediator for the message handler tests: routes the one request type it knows to a real handler,
/// so the tests still prove the handler talks to <see cref="ISender"/> and not to a handler directly.
/// </summary>
internal sealed class SingleHandlerSender(
    IRequestHandler<ONA.Insights.Application.Features.Recommendations.Commands.GenerateRecommendationsCommand,
        IReadOnlyList<ONA.Insights.Application.DTOs.RecommendationDto>> handler) : ISender
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var command = Assert.IsType<ONA.Insights.Application.Features.Recommendations.Commands.GenerateRecommendationsCommand>(request);
        return (Task<TResponse>)(object)handler.Handle(command, cancellationToken);
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest => throw new NotSupportedException();

    public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
