using ONA.Insights.Application.Events;
using ONA.Insights.Application.Features.Recommendations.Commands;
using ONA.Insights.Application.Ports;
using ONA.Insights.Domain.Exceptions;

namespace ONA.Insights.Application.Features.Recommendations.Events;

/// <summary>What the broker consumer should do with the message: commit it or retry later.</summary>
public enum MessageOutcome
{
    /// <summary>Processed; commit.</summary>
    Handled,

    /// <summary>Not processable (bad payload or business rule); commit so it is not redelivered forever.</summary>
    Skipped,

    /// <summary>Technical failure (Analysis unreachable); do NOT commit, retry after a back-off.</summary>
    Retry
}

public sealed record MessageResult(MessageOutcome Outcome, string? Reason = null);

/// <summary>
/// Reaction to <c>AnalysisCompleted</c>: generate recommendations for the run.
/// Sends the command through the mediator (not straight to the handler) so pipeline behaviors apply.
/// Redelivery is safe because <see cref="GenerateRecommendationsCommandHandler"/> is idempotent per run + node.
/// </summary>
public sealed class AnalysisCompletedMessageHandler(ISender sender)
{
    public async Task<MessageResult> Handle(string? json, CancellationToken cancellationToken = default)
    {
        var message = AnalysisCompletedMessage.TryParse(json);
        if (message is null)
        {
            return new MessageResult(MessageOutcome.Skipped, "Invalid AnalysisCompleted payload.");
        }

        try
        {
            await sender.Send(new GenerateRecommendationsCommand(message.AnalysisRunId), cancellationToken);
            return new MessageResult(MessageOutcome.Handled);
        }
        catch (DomainException exception)
        {
            // run not found / not completed / no bottlenecks: redelivery cannot help.
            return new MessageResult(MessageOutcome.Skipped, $"{exception.Code}: {exception.Message}");
        }
        catch (AnalysisIntegrationException exception)
        {
            return new MessageResult(MessageOutcome.Retry, exception.Message);
        }
    }
}
