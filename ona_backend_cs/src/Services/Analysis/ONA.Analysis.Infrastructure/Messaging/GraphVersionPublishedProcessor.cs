using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ONA.Analysis.Application.Abstractions.Messaging;
using ONA.Analysis.Application.Features.AnalysisRuns.Commands;
using ONA.Analysis.Infrastructure.Acl.Events;

namespace ONA.Analysis.Infrastructure.Messaging;

/// <summary>
/// Handles one delivered message. Returns true when the message is finished (commit its offset),
/// false when it must be retried (technical failure – do not commit).
/// </summary>
internal sealed class GraphVersionPublishedProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<GraphVersionPublishedProcessor> logger)
{
    public async Task<bool> ProcessAsync(string? json, CancellationToken cancellationToken)
    {
        if (!GraphEventTranslator.TryParseGraphVersionPublished(json, out var message))
        {
            logger.LogWarning("Skipping unreadable GraphVersionPublished message: {Payload}", json);
            return true;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider
                .GetRequiredService<ICommandHandler<StartAnalysisForGraphVersionCommand, GraphVersionAnalysisOutcome>>();
            var outcome = await handler.HandleAsync(
                new StartAnalysisForGraphVersionCommand(message.BoardId, message.GraphVersionId),
                cancellationToken);

            logger.LogInformation(
                "GraphVersionPublished board {BoardId} version {VersionId}: {Status} {RunId} {Reason}",
                message.BoardId, message.GraphVersionId, outcome.Status, outcome.AnalysisRunId, outcome.Reason);
            return true;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,
                "Analysis for board {BoardId} version {VersionId} failed technically; will retry.",
                message.BoardId, message.GraphVersionId);
            return false;
        }
    }
}
