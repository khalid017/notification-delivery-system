using Microsoft.AspNetCore.Mvc;

namespace NotificationPlatform.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    /// <summary>Liveness check for smoke tests. Doesn't touch the database.</summary>
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });
}
