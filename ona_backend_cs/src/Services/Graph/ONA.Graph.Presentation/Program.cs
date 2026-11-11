using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Services;
using ONA.Graph.Infrastructure;
using ONA.Graph.Presentation.Middleware;
using Microsoft.VisualBasic.FileIO;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "ONA Graph API";
        document.Info.Version = "v1";
        document.Info.Description =
            "Import organizational CSV data and manage network boards, nodes, and weighted connections.";
        return Task.CompletedTask;
    }));
builder.Services.AddScoped<IGraphService, GraphService>();

var graphConnectionString = builder.Configuration.GetConnectionString("Graph")
    ?? throw new InvalidOperationException(
        "Connection string 'Graph' is not configured. Set ConnectionStrings__Graph or use User Secrets.");
builder.Services.AddInfrastructure(graphConnectionString);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

[ApiController]
[Route("api/v1")]
public sealed class OrganizationController : ControllerBase
{
    private static readonly string[] RequiredHeaders =
    [
        "EmpID",
        "FirstName",
        "LastName",
        "Title",
        "DepartmentType",
        "Supervisor"
    ];

    private readonly IGraphService _graphService;

    public OrganizationController(IGraphService graphService)
    {
        _graphService = graphService;
    }

    [HttpPost("data")]
    [ProducesResponseType<OrganizationDataResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrganizationDataResponse>> GetOrganizationData(
        [FromForm] IFormFile? file,
        [FromForm(Name = "board_name")] string? boardName)
    {
        if (file is null)
        {
            return BadRequest(new { detail = "A CSV file is required." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var csv = ReadCsv(stream);
            var employees = MapEmployees(csv);
            var resolvedBoardName = string.IsNullOrWhiteSpace(boardName)
                ? Path.GetFileNameWithoutExtension(Path.GetFileName(file.FileName))
                : boardName.Trim();
            if (string.IsNullOrWhiteSpace(resolvedBoardName))
            {
                resolvedBoardName = "Imported organization";
            }

            var importedBoard = await _graphService.CreateBoardFromImportAsync(
                new ImportBoardDto(resolvedBoardName, employees),
                HttpContext.RequestAborted);
            return Ok(new OrganizationDataResponse(
                "success",
                csv.Records.Count,
                csv.Records,
                importedBoard.Board.Id,
                importedBoard.NodesCreated,
                importedBoard.ConnectionsCreated));
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or MalformedLineException or
                DecoderFallbackException)
        {
            return BadRequest(new { detail = $"Error reading file: {exception.Message}" });
        }
    }

    private static CsvTable ReadCsv(Stream stream)
    {
        using var parser = new TextFieldParser(stream, Encoding.UTF8, true)
        {
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        if (parser.EndOfData)
        {
            return new CsvTable([], []);
        }

        var headers = MakeUniqueHeaders(parser.ReadFields() ?? []);
        var records = new List<IReadOnlyDictionary<string, object?>>();

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields() ?? [];
            if (fields.Length == 1 && fields[0].Length == 0)
            {
                continue;
            }

            if (fields.Length > headers.Length)
            {
                throw new InvalidDataException("A CSV row contains more values than the header.");
            }

            var record = new Dictionary<string, object?>(headers.Length, StringComparer.Ordinal);
            for (var index = 0; index < headers.Length; index++)
            {
                var value = index < fields.Length ? fields[index] : null;
                record.Add(headers[index], ParseValue(value));
            }

            records.Add(record);
        }

        return new CsvTable(headers, records);
    }

    private static IReadOnlyList<ImportedEmployeeDto> MapEmployees(CsvTable csv)
    {
        var columns = RequiredHeaders.ToDictionary(
            header => header,
            header => Array.FindIndex(
                csv.Headers,
                candidate => string.Equals(
                    candidate.Trim(),
                    header,
                    StringComparison.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);
        var missingHeaders = columns.Where(column => column.Value < 0)
            .Select(column => column.Key)
            .ToArray();
        if (missingHeaders.Length > 0)
        {
            throw new InvalidDataException(
                $"CSV is missing required columns: {string.Join(", ", missingHeaders)}.");
        }

        return csv.Records.Select(record => new ImportedEmployeeDto(
            ReadCell(record, RequiredHeaders[0]),
            ReadCell(record, RequiredHeaders[1]),
            ReadCell(record, RequiredHeaders[2]),
            ReadCell(record, RequiredHeaders[3]),
            ReadCell(record, RequiredHeaders[4]),
            ReadCell(record, RequiredHeaders[5]))).ToArray();

        string? ReadCell(IReadOnlyDictionary<string, object?> record, string header)
        {
            var key = csv.Headers[columns[header]];
            return record.TryGetValue(key, out var value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : null;
        }
    }

    private static string[] MakeUniqueHeaders(string[] fields)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        return fields.Select((field, index) =>
        {
            var name = index == 0 ? field.TrimStart('\uFEFF') : field;
            var uniqueName = name;
            var suffix = 1;
            while (!names.Add(uniqueName))
            {
                uniqueName = $"{name}.{suffix++}";
            }

            return uniqueName;
        }).ToArray();
    }

    private static object? ParseValue(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Equals("NaN", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            return integer;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return double.IsFinite(number) ? number : null;
        }

        return value;
    }

    public sealed record OrganizationDataResponse(
        string Status,
        int TotalRows,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> Data,
        string BoardId,
        int NodesCreated,
        int ConnectionsCreated);

    private sealed record CsvTable(
        string[] Headers,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> Records);
}

public partial class Program
{
}
