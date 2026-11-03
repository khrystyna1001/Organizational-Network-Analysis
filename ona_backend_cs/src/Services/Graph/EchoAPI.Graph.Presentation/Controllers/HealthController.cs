using Microsoft.AspNetCore.Mvc;

namespace EchoAPI.Graph.Presentation.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [HttpGet("/")]
    public IActionResult Get() =>
        Ok(new
        {
            status = "online",
            message = "ONA Engine with Microsoft Graph & Gemini AI is ready."
        });
}
