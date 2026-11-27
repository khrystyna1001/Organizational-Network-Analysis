using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Features.Nodes.Commands;
using ONA.Graph.Application.Features.Nodes.Queries;
using ONA.Graph.Presentation.Contracts;

namespace ONA.Graph.Presentation.Controllers;

/// <summary>Nodes (people) of a board. Every endpoint delegates to exactly one CQRS handler.</summary>
[ApiController]
[Authorize]
[Route("api/v1/nodes")]
public sealed class NodesController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<NodeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<NodeDto>> CreateNode(
        CreateNodeRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var node = await sender.Send(new AddNodeCommand(request.BoardId, request.ToDto()), cancellationToken);
        return CreatedAtAction(nameof(GetNode), new { nodeId = node.Id }, node);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NodeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NodeDto>>> GetNodes(
        [FromServices] ISender sender,
        [FromQuery(Name = "board_id")] string? boardId = null,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetNodesQuery(boardId), cancellationToken));

    [HttpGet("{nodeId}", Name = nameof(GetNode))]
    [ProducesResponseType<NodeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NodeDto>> GetNode(
        string nodeId,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var node = await sender.Send(new GetNodeByIdQuery(nodeId), cancellationToken);
        return node is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Node not found")
            : Ok(node);
    }

    [HttpPut("{nodeId}")]
    [ProducesResponseType<NodeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<NodeDto>> UpdateNode(
        string nodeId,
        UpdateNodeRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var node = await sender.Send(new UpdateNodeCommand(nodeId, request.ToDto()), cancellationToken);
        return node is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Node not found")
            : Ok(node);
    }

    [HttpDelete("{nodeId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNode(
        string nodeId,
        [FromServices] ISender sender,
        CancellationToken cancellationToken) =>
        await sender.Send(new DeleteNodeCommand(nodeId), cancellationToken)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Node not found");
}
