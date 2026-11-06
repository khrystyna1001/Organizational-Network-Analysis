using Microsoft.AspNetCore.Mvc;

namespace ONA.Graph.Presentation.Controllers;

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
