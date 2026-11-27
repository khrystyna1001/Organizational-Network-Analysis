using ONA.Insights.Application.Abstractions;
using ONA.Insights.Application.DTOs;
using ONA.Insights.Domain.Aggregates;
using ONA.Insights.Domain.Exceptions;
using ONA.Insights.Domain.ValueObjects;

namespace ONA.Insights.Application.Features.Recommendations.Queries;

/// <summary>READ (list): optional filters by analysis run and status (Delivered, Approved, Rejected).</summary>
public sealed record GetRecommendationsQuery(string? AnalysisRunId, string? Status)
    : IRequest<IReadOnlyList<RecommendationDto>>;

public sealed class GetRecommendationsQueryHandler(IRecommendationRepository repository)
    : IRequestHandler<GetRecommendationsQuery, IReadOnlyList<RecommendationDto>>
{
    public async Task<IReadOnlyList<RecommendationDto>> Handle(
        GetRecommendationsQuery query,
        CancellationToken cancellationToken = default)
    {
        RecommendationStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<RecommendationStatus>(query.Status, ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                throw new InvalidRecommendationResponseException(
                    "Status must be one of: delivered, approved, rejected.");
            }

            status = parsed;
        }

        var runId = string.IsNullOrWhiteSpace(query.AnalysisRunId) ? null : new AnalysisRunId(query.AnalysisRunId);
        var items = await repository.ListAsync(runId, status, cancellationToken);
        return items.Select(RecommendationDto.FromDomain).ToList();
    }
}
