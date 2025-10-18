using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TaskMasterApi.Controllers;

[ApiController]
[Route("api/v1/system")]
[Authorize]
public sealed class SystemController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { message = "pong" });
}