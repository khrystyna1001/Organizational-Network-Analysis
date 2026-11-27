using ONA.Insights.Application.Abstractions;
using ONA.Insights.Application.DTOs;
using ONA.Insights.Application.Notifications;
using ONA.Insights.Domain.Exceptions;
using ONA.Insights.Domain.ValueObjects;

namespace ONA.Insights.Application.Features.Recommendations.Commands;

/// <summary>
/// RespondToLLM: the user approves or rejects once. Approval only records the decision;
/// requesting a Graph update (ApplyGraphUpdate) is a documented target, not implemented.
/// Publishes <see cref="RecommendationRespondedNotification"/> after the decision is saved.
/// </summary>
public sealed record RespondToRecommendationCommand(
    string RecommendationId,
    string? Response,
    string? Comment,
    string? UserId) : IRequest<RecommendationDto>;

public sealed class RespondToRecommendationCommandHandler(
    IRecommendationRepository repository,
    TimeProvider clock,
    IPublisher notifications)
    : IRequestHandler<RespondToRecommendationCommand, RecommendationDto>
{
    public async Task<RecommendationDto> Handle(
        RespondToRecommendationCommand command,
        CancellationToken cancellationToken = default)
    {
        var id = new RecommendationId(command.RecommendationId);
        var recommendation = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new RecommendationNotFoundException(id.Value);

        var now = clock.GetUtcNow().UtcDateTime;
        switch (command.Response?.Trim().ToLowerInvariant())
        {
            case "approve":
                recommendation.Approve(command.Comment, command.UserId, now);
                break;
            case "reject":
                recommendation.Reject(command.Comment, command.UserId, now);
                break;
            default:
                throw new InvalidRecommendationResponseException("Response must be 'approve' or 'reject'.");
        }

        await repository.SaveAsync(recommendation, cancellationToken);

        var dto = RecommendationDto.FromDomain(recommendation);
        await NotifyAsync(dto, cancellationToken);
        return dto;
    }

    /// <summary>Best-effort UI push: the decision is saved, a failing handler must not undo that.</summary>
    private async Task NotifyAsync(RecommendationDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await notifications.Publish(
                new RecommendationRespondedNotification(dto.Id, dto.AnalysisRunId, dto.BoardId, dto.Status),
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Live updates are optional.
        }
    }
}
