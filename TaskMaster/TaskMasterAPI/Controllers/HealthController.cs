using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TaskMasterApi.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(HealthCheckService healthChecks) : ControllerBase
{
    [HttpGet("live")]
    [AllowAnonymous]
    public IActionResult Live() => Ok(new { status = "Healthy" });

    [HttpGet("ready")]
    [AllowAnonymous]
    public async Task<IActionResult> Ready()
    {
        var report = await healthChecks.CheckHealthAsync();
        if (report.Status == HealthStatus.Healthy)
            return Ok(new { status = "Healthy" });

        var payload = new
        {
            status = report.Status.ToString(),
            entries = report.Entries.ToDictionary(k => k.Key, v => new
            {
                status = v.Value.Status.ToString(),
                description = v.Value.Description
            })
        };

        return StatusCode(StatusCodes.Status503ServiceUnavailable, payload);
    }
}