using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Services;
using ONA.Graph.Presentation.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ONA.Graph.Presentation.Controllers;

[ApiController]
[Route("api/v1/boards")]
public sealed class BoardsController(IGraphService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<BoardDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BoardDto>> CreateBoard(
        CreateBoardRequest request,
        CancellationToken cancellationToken)
    {
        var board = await service.CreateBoardAsync(request.ToDto(), cancellationToken);
        return CreatedAtAction(nameof(GetBoard), new { boardId = board.Id }, board);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BoardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BoardDto>>> GetBoards(
        CancellationToken cancellationToken) =>
        Ok(await service.GetBoardsAsync(cancellationToken));

    [HttpGet("{boardId}", Name = nameof(GetBoard))]
    [ProducesResponseType<BoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BoardDto>> GetBoard(
        string boardId,
        CancellationToken cancellationToken)
    {
        var board = await service.GetBoardAsync(boardId, cancellationToken);
        return board is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Board not found")
            : Ok(board);
    }

    [HttpPut("{boardId}")]
    [ProducesResponseType<BoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BoardDto>> UpdateBoard(
        string boardId,
        UpdateBoardRequest request,
        CancellationToken cancellationToken)
    {
        var board = await service.UpdateBoardAsync(
            boardId,
            request.ToDto(),
            cancellationToken);
        return board is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Board not found")
            : Ok(board);
    }

    [HttpDelete("{boardId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBoard(
        string boardId,
        CancellationToken cancellationToken) =>
        await service.DeleteBoardAsync(boardId, cancellationToken)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Board not found");
}
