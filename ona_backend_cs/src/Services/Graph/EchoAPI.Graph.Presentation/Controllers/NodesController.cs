using Microsoft.AspNetCore.Mvc;
using EchoAPI.Graph.Application.DTOs;
using EchoAPI.Graph.Application.Services;
using EchoAPI.Graph.Presentation.Contracts;

namespace EchoAPI.Graph.Presentation.Controllers;

[ApiController]
[Route("api/v1/nodes")]
public sealed class NodesController(IGraphService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<NodeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<NodeDto>> CreateNode(
        CreateNodeRequest request,
        CancellationToken cancellationToken)
    {
        var node = await service.AddNodeAsync(
            request.BoardId,
            request.ToDto(),
            cancellationToken);
        return CreatedAtAction(nameof(GetNode), new { nodeId = node.Id }, node);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NodeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NodeDto>>> GetNodes(
        [FromQuery(Name = "board_id")] string? boardId = null,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetNodesAsync(boardId, cancellationToken));

    [HttpGet("{nodeId}", Name = nameof(GetNode))]
    [ProducesResponseType<NodeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NodeDto>> GetNode(
        string nodeId,
        CancellationToken cancellationToken)
    {
        var node = await service.GetNodeAsync(nodeId, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var node = await service.UpdateNodeAsync(nodeId, request.ToDto(), cancellationToken);
        return node is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Node not found")
            : Ok(node);
    }

    [HttpDelete("{nodeId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNode(
        string nodeId,
        CancellationToken cancellationToken) =>
        await service.DeleteNodeAsync(nodeId, cancellationToken)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Node not found");
}
