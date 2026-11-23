using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ONA.Insights.Presentation.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("/")]
    public IActionResult Get() => Ok(new { service = "ONA Insights API", status = "ok" });
}
