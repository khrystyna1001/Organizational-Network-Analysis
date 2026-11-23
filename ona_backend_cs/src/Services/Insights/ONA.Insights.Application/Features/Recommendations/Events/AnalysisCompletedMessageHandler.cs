using ONA.Insights.Application.Abstractions.Messaging;
using ONA.Insights.Application.DTOs;
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
/// Redelivery is safe because <see cref="GenerateRecommendationsCommandHandler"/> is idempotent per run + node.
/// </summary>
public sealed class AnalysisCompletedMessageHandler(
    ICommandHandler<GenerateRecommendationsCommand, IReadOnlyList<RecommendationDto>> generate)
{
    public async Task<MessageResult> HandleAsync(string? json, CancellationToken cancellationToken = default)
    {
        var message = AnalysisCompletedMessage.TryParse(json);
        if (message is null)
        {
            return new MessageResult(MessageOutcome.Skipped, "Invalid AnalysisCompleted payload.");
        }

        try
        {
            await generate.HandleAsync(new GenerateRecommendationsCommand(message.AnalysisRunId), cancellationToken);
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
