using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ONA.Analysis.Presentation.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("/")]
    public IActionResult Get() => Ok(new { status = "online", service = "ONA Analysis API" });
}
