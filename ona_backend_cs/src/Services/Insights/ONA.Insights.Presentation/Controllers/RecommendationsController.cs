using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ONA.Insights.Application.DTOs;
using ONA.Insights.Application.Features.Recommendations.Commands;
using ONA.Insights.Application.Features.Recommendations.Queries;
using ONA.Insights.Presentation.Contracts;

namespace ONA.Insights.Presentation.Controllers;

/// <summary>Every endpoint delegates to exactly one CQRS handler.</summary>
[ApiController]
[Authorize]
[Route("api/v1/recommendations")]
public sealed class RecommendationsController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<IReadOnlyList<RecommendationDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<IReadOnlyList<RecommendationDto>>> Generate(
        GenerateRecommendationsRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GenerateRecommendationsCommand(request.AnalysisRunId), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RecommendationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RecommendationDto>>> List(
        [FromQuery(Name = "analysis_run_id")] string? analysisRunId,
        [FromQuery] string? status,
        [FromServices] ISender sender,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetRecommendationsQuery(analysisRunId, status), cancellationToken));

    [HttpGet("{recommendationId}")]
    [ProducesResponseType<RecommendationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecommendationDto>> Get(
        string recommendationId,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRecommendationByIdQuery(recommendationId), cancellationToken);
        return result is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Recommendation not found")
            : Ok(result);
    }

    /// <summary>Approve or reject. The responder is the token's <c>sub</c>; the Service role may not respond.</summary>
    [HttpPost("{recommendationId}/respond")]
    [Authorize(Roles = "User,Admin")]
    [ProducesResponseType<RecommendationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RecommendationDto>> Respond(
        string recommendationId,
        RespondToRecommendationRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(
            new RespondToRecommendationCommand(recommendationId, request.Response, request.Comment, User.FindFirst("sub")?.Value),
            cancellationToken));
}
