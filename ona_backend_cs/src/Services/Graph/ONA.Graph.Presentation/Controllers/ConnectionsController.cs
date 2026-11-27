using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Features.Connections.Commands;
using ONA.Graph.Application.Features.Connections.Queries;
using ONA.Graph.Presentation.Contracts;

namespace ONA.Graph.Presentation.Controllers;

/// <summary>Directed, weighted connections of a board. Every endpoint delegates to exactly one CQRS handler.</summary>
[ApiController]
[Authorize]
[Route("api/v1/connections")]
public sealed class ConnectionsController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ConnectionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ConnectionDto>> CreateConnection(
        CreateConnectionRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var connection = await sender.Send(
            new AddConnectionCommand(request.BoardId, request.ToDto()),
            cancellationToken);
        return CreatedAtAction(
            nameof(GetConnection),
            new { connectionId = connection.Id },
            connection);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ConnectionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConnectionDto>>> GetConnections(
        [FromServices] ISender sender,
        [FromQuery(Name = "board_id")] string? boardId = null,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetConnectionsQuery(boardId), cancellationToken));

    [HttpGet("{connectionId}", Name = nameof(GetConnection))]
    [ProducesResponseType<ConnectionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConnectionDto>> GetConnection(
        string connectionId,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var connection = await sender.Send(new GetConnectionByIdQuery(connectionId), cancellationToken);
        return connection is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Connection not found")
            : Ok(connection);
    }

    [HttpPut("{connectionId}")]
    [ProducesResponseType<ConnectionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ConnectionDto>> UpdateConnection(
        string connectionId,
        UpdateConnectionRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var connection = await sender.Send(
            new UpdateConnectionCommand(connectionId, request.ToDto()),
            cancellationToken);
        return connection is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Connection not found")
            : Ok(connection);
    }

    [HttpDelete("{connectionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConnection(
        string connectionId,
        [FromServices] ISender sender,
        CancellationToken cancellationToken) =>
        await sender.Send(new DeleteConnectionCommand(connectionId), cancellationToken)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Connection not found");
}
