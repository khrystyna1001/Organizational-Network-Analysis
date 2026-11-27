using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Features.Boards.Commands;
using ONA.Graph.Application.Features.Boards.Queries;
using ONA.Graph.Presentation.Contracts;

namespace ONA.Graph.Presentation.Controllers;

/// <summary>
/// Board CRUD + lifecycle. Every endpoint delegates to exactly one CQRS handler:
/// writes go through the Board aggregate (tracked), reads use AsNoTracking projections.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/boards")]
public sealed class BoardsController : ControllerBase
{
    // CREATE
    [HttpPost]
    [ProducesResponseType<BoardDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BoardDto>> CreateBoard(
        CreateBoardRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var board = await sender.Send(
            new CreateBoardCommand(request.Name, request.Description),
            cancellationToken);
        return CreatedAtAction(nameof(GetBoard), new { boardId = board.Id }, board);
    }

    // READ (list)
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BoardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BoardDto>>> GetBoards(
        [FromServices] ISender sender,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetAllBoardsQuery(), cancellationToken));

    // READ (single)
    [HttpGet("{boardId}", Name = nameof(GetBoard))]
    [ProducesResponseType<BoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BoardDto>> GetBoard(
        string boardId,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var board = await sender.Send(new GetBoardByIdQuery(boardId), cancellationToken);
        return board is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Board not found")
            : Ok(board);
    }

    // READ (snapshot of the current GraphVersion, consumed by Analysis)
    [HttpGet("{boardId}/snapshot")]
    [ProducesResponseType<GraphSnapshotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<GraphSnapshotDto>> GetSnapshot(
        string boardId,
        [FromServices] ISender sender,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetGraphSnapshotQuery(boardId), cancellationToken));

    // UPDATE (details)
    [HttpPut("{boardId}")]
    [ProducesResponseType<BoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BoardDto>> UpdateBoard(
        string boardId,
        UpdateBoardRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(
            new RenameBoardCommand(boardId, request.Name, request.Description, request.DescriptionSpecified),
            cancellationToken));

    // UPDATE (domain command) Draft -> Built
    [HttpPost("{boardId}/build")]
    [ProducesResponseType<BoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BoardDto>> BuildBoard(
        string boardId,
        [FromServices] ISender sender,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new BuildBoardGraphCommand(boardId), cancellationToken));

    // UPDATE (domain command) Built -> Published
    [HttpPost("{boardId}/publish")]
    [ProducesResponseType<BoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BoardDto>> PublishBoard(
        string boardId,
        [FromServices] ISender sender,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new PublishBoardCommand(boardId), cancellationToken));

    // UPDATE (domain command) Published -> Archived
    [HttpPost("{boardId}/archive")]
    [ProducesResponseType<BoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BoardDto>> ArchiveBoard(
        string boardId,
        [FromServices] ISender sender,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ArchiveBoardCommand(boardId), cancellationToken));

    // DELETE (guarded: Published boards must be archived first)
    [HttpDelete("{boardId}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteBoard(
        string boardId,
        [FromServices] ISender sender,
        CancellationToken cancellationToken) =>
        await sender.Send(new DeleteBoardCommand(boardId), cancellationToken)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Board not found");
}
