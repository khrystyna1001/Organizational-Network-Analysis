using ONA.Insights.Application.Abstractions;
using ONA.Insights.Application.DTOs;
using ONA.Insights.Domain.ValueObjects;

namespace ONA.Insights.Application.Features.Recommendations.Queries;

/// <summary>READ (single): no change tracking, returns a DTO or null.</summary>
public sealed record GetRecommendationByIdQuery(string RecommendationId) : IRequest<RecommendationDto?>;

public sealed class GetRecommendationByIdQueryHandler(IRecommendationRepository repository)
    : IRequestHandler<GetRecommendationByIdQuery, RecommendationDto?>
{
    public async Task<RecommendationDto?> Handle(
        GetRecommendationByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var recommendation = await repository.GetByIdReadOnlyAsync(
            new RecommendationId(query.RecommendationId), cancellationToken);
        return recommendation is null ? null : RecommendationDto.FromDomain(recommendation);
    }
}
