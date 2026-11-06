using Microsoft.AspNetCore.Mvc;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Services;
using ONA.Graph.Presentation.Contracts;

namespace ONA.Graph.Presentation.Controllers;

[ApiController]
[Route("api/v1/connections")]
public sealed class ConnectionsController(IGraphService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ConnectionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ConnectionDto>> CreateConnection(
        CreateConnectionRequest request,
        CancellationToken cancellationToken)
    {
        var connection = await service.AddConnectionAsync(
            request.BoardId,
            request.ToDto(),
            cancellationToken);
        return CreatedAtAction(
            nameof(GetConnection),
            new { connectionId = connection.Id },
            connection);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ConnectionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConnectionDto>>> GetConnections(
        [FromQuery(Name = "board_id")] string? boardId = null,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetConnectionsAsync(boardId, cancellationToken));

    [HttpGet("{connectionId}", Name = nameof(GetConnection))]
    [ProducesResponseType<ConnectionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConnectionDto>> GetConnection(
        string connectionId,
        CancellationToken cancellationToken)
    {
        var connection = await service.GetConnectionAsync(connectionId, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var connection = await service.UpdateConnectionAsync(
            connectionId,
            request.ToDto(),
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
        CancellationToken cancellationToken) =>
        await service.DeleteConnectionAsync(connectionId, cancellationToken)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Connection not found");
}
