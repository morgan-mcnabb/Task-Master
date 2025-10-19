using System.Globalization;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskMasterApi.Contracts.Auth;

namespace TaskMasterApi.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    ApplicationDbContext dbContext)
    : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Username and password are required." });

        var existing = await userManager.FindByNameAsync(request.UserName);
        if (existing is not null)
            return Conflict(new { error = "User already exists." });

        var user = new IdentityUser { UserName = request.UserName };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                error = "Registration failed.",
                details = result.Errors.Select(e => $"{e.Code}:{e.Description}")
            });
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return Created("/auth/me", new { user = request.UserName });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Username and password are required." });

        var signInResult = await signInManager.PasswordSignInAsync(
            userName: request.UserName,
            password: request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (signInResult.Succeeded)
        {
            return Ok(new { status = "ok" });
        }

        if (signInResult.IsLockedOut)
        {
            // Provide Retry-After when we can infer remaining lockout time
            var user = await userManager.FindByNameAsync(request.UserName);
            if (user is not null)
            {
                var lockoutEndUtc = await userManager.GetLockoutEndDateAsync(user);
                if (lockoutEndUtc.HasValue)
                {
                    var secondsRemaining = (int)Math.Max(
                        Math.Ceiling((lockoutEndUtc.Value - DateTimeOffset.UtcNow).TotalSeconds),
                        0);

                    Response.Headers["Retry-After"] = secondsRemaining.ToString(CultureInfo.InvariantCulture);
                }
            }

            return StatusCode(StatusCodes.Status423Locked, new { error = "Account is temporarily locked due to multiple failed attempts. Try again later." });
        }

        // Avoid account enumeration: do not disclose whether username or password was incorrect.
        return Unauthorized();
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrEmpty(userName)) return NotFound();

        var user = await userManager.FindByNameAsync(userName);
        if (user is null) return NotFound();

        var settings = await dbContext.UserSettings
            .Where(s => s.UserId == user.Id)
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        return Ok(new { userName = user.UserName, settings });
    }

    [HttpPut("settings")]
    [Authorize]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request)
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrEmpty(userName)) return Unauthorized();

        var user = await userManager.FindByNameAsync(userName);
        if (user is null) return NotFound();

        var existing = await dbContext.UserSettings
            .Where(s => s.UserId == user.Id)
            .ToListAsync();

        dbContext.UserSettings.RemoveRange(existing);

        if (request.Settings is not null)
        {
            foreach (var keyValuePair in request.Settings)
            {
                dbContext.UserSettings.Add(new Domain.Users.UserSetting
                {
                    UserId = user.Id,
                    Key = keyValuePair.Key,
                    Value = keyValuePair.Value
                });
            }
        }

        await dbContext.SaveChangesAsync();
        return NoContent();
    }
}
