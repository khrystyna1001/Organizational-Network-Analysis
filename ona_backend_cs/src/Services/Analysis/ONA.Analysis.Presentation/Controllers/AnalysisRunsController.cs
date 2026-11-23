using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ONA.Analysis.Application.Abstractions.Messaging;
using ONA.Analysis.Application.DTOs;
using ONA.Analysis.Application.Features.AnalysisRuns.Commands;
using ONA.Analysis.Application.Features.AnalysisRuns.Queries;
using ONA.Analysis.Presentation.Contracts;

namespace ONA.Analysis.Presentation.Controllers;

/// <summary>Every endpoint delegates to exactly one CQRS handler.</summary>
[ApiController]
[Authorize]
[Route("api/v1/analysis-runs")]
public sealed class AnalysisRunsController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<AnalysisRunDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<AnalysisRunDto>> StartAnalysisRun(
        StartAnalysisRunRequest request,
        [FromServices] ICommandHandler<StartAnalysisRunCommand, AnalysisRunDto> handler,
        CancellationToken cancellationToken)
    {
        var run = await handler.HandleAsync(new StartAnalysisRunCommand(request.BoardId ?? string.Empty), cancellationToken);
        return CreatedAtAction(nameof(GetAnalysisRun), new { analysisRunId = run.Id }, run);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AnalysisRunDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AnalysisRunDto>>> GetAnalysisRuns(
        [FromQuery(Name = "board_id")] string? boardId,
        [FromServices] IQueryHandler<GetAnalysisRunsQuery, IReadOnlyList<AnalysisRunDto>> handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new GetAnalysisRunsQuery(boardId), cancellationToken));

    [HttpGet("{analysisRunId}", Name = nameof(GetAnalysisRun))]
    [ProducesResponseType<AnalysisRunDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnalysisRunDto>> GetAnalysisRun(
        string analysisRunId,
        [FromServices] IQueryHandler<GetAnalysisRunByIdQuery, AnalysisRunDto?> handler,
        CancellationToken cancellationToken)
    {
        var run = await handler.HandleAsync(new GetAnalysisRunByIdQuery(analysisRunId), cancellationToken);
        return run is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Analysis run not found")
            : Ok(run);
    }
}
