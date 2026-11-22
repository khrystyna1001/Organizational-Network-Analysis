using Microsoft.AspNetCore.Mvc;
using ONA.Graph.Application.Abstractions.Messaging;
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
[Route("api/v1/boards")]
public sealed class BoardsController : ControllerBase
{
    // CREATE
    [HttpPost]
    [ProducesResponseType<BoardDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BoardDto>> CreateBoard(
        CreateBoardRequest request,
        [FromServices] ICommandHandler<CreateBoardCommand, BoardDto> handler,
        CancellationToken cancellationToken)
    {
        var board = await handler.HandleAsync(
            new CreateBoardCommand(request.Name, request.Description),
            cancellationToken);
        return CreatedAtAction(nameof(GetBoard), new { boardId = board.Id }, board);
    }

    // READ (list)
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BoardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BoardDto>>> GetBoards(
        [FromServices] IQueryHandler<GetAllBoardsQuery, IReadOnlyList<BoardDto>> handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new GetAllBoardsQuery(), cancellationToken));

    // READ (single)
    [HttpGet("{boardId}", Name = nameof(GetBoard))]
    [ProducesResponseType<BoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BoardDto>> GetBoard(
        string boardId,
        [FromServices] IQueryHandler<GetBoardByIdQuery, BoardDto?> handler,
        CancellationToken cancellationToken)
    {
        var board = await handler.HandleAsync(new GetBoardByIdQuery(boardId), cancellationToken);
        return board is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Board not found")
            : Ok(board);
    }

    // UPDATE (details)
    [HttpPut("{boardId}")]
    [ProducesResponseType<BoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BoardDto>> UpdateBoard(
        string boardId,
        UpdateBoardRequest request,
        [FromServices] ICommandHandler<RenameBoardCommand, BoardDto> handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(
            new RenameBoardCommand(boardId, request.Name, request.Description, request.DescriptionSpecified),
            cancellationToken));

    // UPDATE (domain command) Draft -> Built
    [HttpPost("{boardId}/build")]
    [ProducesResponseType<BoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BoardDto>> BuildBoard(
        string boardId,
        [FromServices] ICommandHandler<BuildBoardGraphCommand, BoardDto> handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new BuildBoardGraphCommand(boardId), cancellationToken));

    // UPDATE (domain command) Built -> Published
    [HttpPost("{boardId}/publish")]
    [ProducesResponseType<BoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BoardDto>> PublishBoard(
        string boardId,
        [FromServices] ICommandHandler<PublishBoardCommand, BoardDto> handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new PublishBoardCommand(boardId), cancellationToken));

    // UPDATE (domain command) Published -> Archived
    [HttpPost("{boardId}/archive")]
    [ProducesResponseType<BoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BoardDto>> ArchiveBoard(
        string boardId,
        [FromServices] ICommandHandler<ArchiveBoardCommand, BoardDto> handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new ArchiveBoardCommand(boardId), cancellationToken));

    // DELETE (guarded: Published boards must be archived first)
    [HttpDelete("{boardId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteBoard(
        string boardId,
        [FromServices] ICommandHandler<DeleteBoardCommand, bool> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new DeleteBoardCommand(boardId), cancellationToken)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Board not found");
}
