using Microsoft.AspNetCore.Mvc;
using ONA.Graph.Application.Abstractions.Messaging;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Features.Boards.Commands;

namespace ONA.Graph.Presentation.Controllers;

/// <summary>
/// Uploads an HRIS employee export and creates a Draft board from it.
/// Parsing and translation of the foreign HRIS format happen in Infrastructure/Acl
/// (IEmployeeDirectoryPort); this controller only handles HTTP concerns.
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class OrganizationImportController : ControllerBase
{
    private const string FallbackBoardName = "Imported organization";

    [HttpPost("data")]
    [ProducesResponseType<OrganizationDataResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrganizationDataResponse>> ImportOrganization(
        [FromForm] IFormFile? file,
        [FromForm(Name = "board_name")] string? boardName,
        [FromServices] ICommandHandler<ImportBoardFromHrisCommand, ImportedBoardDto> handler,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "A CSV file is required.");
        }

        await using var stream = file.OpenReadStream();
        var imported = await handler.HandleAsync(
            new ImportBoardFromHrisCommand(ResolveBoardName(boardName, file.FileName), stream),
            cancellationToken);

        return Ok(new OrganizationDataResponse(
            "success",
            imported.Members.Count,
            imported.Members,
            imported.Board.Id,
            imported.NodesCreated,
            imported.ConnectionsCreated));
    }

    private static string ResolveBoardName(string? boardName, string? fileName)
    {
        if (!string.IsNullOrWhiteSpace(boardName))
        {
            return boardName.Trim();
        }

        var fromFile = Path.GetFileNameWithoutExtension(Path.GetFileName(fileName));
        return string.IsNullOrWhiteSpace(fromFile) ? FallbackBoardName : fromFile;
    }

    public sealed record OrganizationDataResponse(
        string Status,
        int TotalRows,
        IReadOnlyList<ImportedMemberDto> Data,
        string BoardId,
        int NodesCreated,
        int ConnectionsCreated);
}
