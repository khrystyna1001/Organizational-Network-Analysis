using ONA.Insights.Application.Abstractions;
using ONA.Insights.Application.Abstractions.Messaging;
using ONA.Insights.Application.DTOs;
using ONA.Insights.Domain.Exceptions;
using ONA.Insights.Domain.ValueObjects;

namespace ONA.Insights.Application.Features.Recommendations.Commands;

/// <summary>
/// RespondToLLM: the user approves or rejects once. Approval only records the decision;
/// requesting a Graph update (ApplyGraphUpdate) is a documented target, not implemented.
/// </summary>
public sealed record RespondToRecommendationCommand(
    string RecommendationId,
    string? Response,
    string? Comment,
    string? UserId) : ICommand<RecommendationDto>;

public sealed class RespondToRecommendationCommandHandler(
    IRecommendationRepository repository,
    TimeProvider clock)
    : ICommandHandler<RespondToRecommendationCommand, RecommendationDto>
{
    public async Task<RecommendationDto> HandleAsync(
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
        return RecommendationDto.FromDomain(recommendation);
    }
}
