using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic.FileIO;
using ONA.Organization.Application.DTOs;
using ONA.Organization.Application.Services;

namespace ONA.Organization.Presentation.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class OrganizationController(OrganizationDataService service) : ControllerBase
{
    [HttpPost("data")]
    [ProducesResponseType<OrganizationDataResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<OrganizationDataResponse> GetOrganizationData(
        [FromForm] IFormFile? file)
    {
        if (file is null)
        {
            return BadRequest(new { detail = "A CSV file is required." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var result = service.ReadCsv(stream);
            return Ok(new OrganizationDataResponse("success", result.TotalRows, result.Data));
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or MalformedLineException or
                DecoderFallbackException)
        {
            return BadRequest(new { detail = $"Error reading file: {exception.Message}" });
        }
    }

    public sealed record OrganizationDataResponse(
        string Status,
        int TotalRows,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> Data);
}
