using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ONA.Graph.Presentation.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    [HttpGet("/")]
    public IActionResult Get() =>
        Ok(new
        {
            status = "online",
            service = "ONA Graph API"
        });
}
