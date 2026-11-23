using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ONA.Graph.Application.Abstractions.Messaging;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Features.Boards.Commands;
using ONA.Graph.Application.Features.Boards.Queries;
using ONA.Graph.Application.Ports;
using ONA.Graph.Presentation.Contracts;
using System.Text.Json;

namespace ONA.Graph.Presentation.Controllers;

/// <summary>
/// Uploads a people table (the HRIS employee export, or any CSV plus a column mapping) and creates a Draft board.
/// Parsing and translation of the foreign HRIS format happen in Infrastructure/Acl
/// (IEmployeeDirectoryPort); this controller only handles HTTP concerns.
/// </summary>
[ApiController]
[Authorize]
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
        [FromForm(Name = "mapping")] string? mapping,
        [FromServices] ICommandHandler<ImportBoardFromHrisCommand, ImportedBoardDto> handler,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "A CSV file is required.");
        }

        ColumnMapping? columnMapping;
        try
        {
            columnMapping = ParseMapping(mapping);
        }
        catch (JsonException)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "The column mapping must be valid JSON.");
        }

        await using var stream = file.OpenReadStream();
        var imported = await handler.HandleAsync(
            new ImportBoardFromHrisCommand(ResolveBoardName(boardName, file.FileName), stream, columnMapping),
            cancellationToken);

        return Ok(new OrganizationDataResponse(
            "success",
            imported.Members.Count,
            imported.Members,
            imported.Board.Id,
            imported.NodesCreated,
            imported.ConnectionsCreated));
    }

    /// <summary>First rows, columns and a suggested column mapping of a CSV. Nothing is saved.</summary>
    [HttpPost("data/preview")]
    [ProducesResponseType<CsvPreview>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CsvPreview>> PreviewCsv(
        [FromForm] IFormFile? file,
        [FromServices] IQueryHandler<PreviewCsvQuery, CsvPreview> handler,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "A CSV file is required.");
        }

        await using var stream = file.OpenReadStream();
        return Ok(await handler.HandleAsync(new PreviewCsvQuery(stream), cancellationToken));
    }

    private static readonly JsonSerializerOptions MappingJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static ColumnMapping? ParseMapping(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<ColumnMappingRequest>(json, MappingJson)?.ToDomain();

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
