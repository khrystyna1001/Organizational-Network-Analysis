using ONA.Analysis.Application.Services;
using ONA.Analysis.Domain.Exceptions;

namespace ONA.Analysis.Application.Features.AnalysisRuns.Commands;

/// <summary>Triggered by the Graph event GraphVersionPublished (at-least-once delivery).</summary>
public sealed record StartAnalysisForGraphVersionCommand(string BoardId, string GraphVersionId)
    : IRequest<GraphVersionAnalysisOutcome>;

/// <summary>What happened with one delivered event. <see cref="Status"/> is Analyzed, AlreadyAnalyzed or Skipped.</summary>
public sealed record GraphVersionAnalysisOutcome(string Status, string? AnalysisRunId, string? Reason)
{
    public const string Analyzed = "Analyzed";
    public const string AlreadyAnalyzed = "AlreadyAnalyzed";
    public const string Skipped = "Skipped";
}

/// <summary>
/// Business failures (board gone, version not published, invalid graph) are reported as Skipped:
/// retrying cannot help. Technical failures (e.g. <see cref="Ports.GraphIntegrationException"/>) propagate
/// so the consumer does not commit the message and retries later.
/// </summary>
public sealed class StartAnalysisForGraphVersionCommandHandler(AnalysisRunService service)
    : IRequestHandler<StartAnalysisForGraphVersionCommand, GraphVersionAnalysisOutcome>
{
    public async Task<GraphVersionAnalysisOutcome> Handle(
        StartAnalysisForGraphVersionCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var run = await service.StartForGraphVersionAsync(command.BoardId, command.GraphVersionId, cancellationToken);
            return run is null
                ? new GraphVersionAnalysisOutcome(GraphVersionAnalysisOutcome.AlreadyAnalyzed, null, null)
                : new GraphVersionAnalysisOutcome(GraphVersionAnalysisOutcome.Analyzed, run.Id, null);
        }
        catch (DomainException exception)
        {
            return new GraphVersionAnalysisOutcome(GraphVersionAnalysisOutcome.Skipped, null, $"{exception.Code}: {exception.Message}");
        }
    }
}
