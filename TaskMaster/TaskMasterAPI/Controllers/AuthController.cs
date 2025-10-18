using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskMasterApi.Contracts.Auth;

namespace TaskMasterApi.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ApplicationDbContext _dbContext;

    public AuthController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        ApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _dbContext = dbContext;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Username and password are required." });

        var existing = await _userManager.FindByNameAsync(request.UserName);
        if (existing is not null)
            return Conflict(new { error = "User already exists." });

        var user = new IdentityUser { UserName = request.UserName };
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                error = "Registration failed.",
                details = result.Errors.Select(e => $"{e.Code}:{e.Description}")
            });
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        return Created("/auth/me", new { user = request.UserName });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByNameAsync(request.UserName ?? string.Empty);
        if (user is null) return Unauthorized();

        var valid = await _userManager.CheckPasswordAsync(user, request.Password ?? string.Empty);
        if (!valid) return Unauthorized();

        await _signInManager.SignInAsync(user, isPersistent: false);
        return Ok(new { status = "ok" });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrEmpty(userName)) return NotFound();

        var user = await _userManager.FindByNameAsync(userName);
        if (user is null) return NotFound();

        var settings = await _dbContext.UserSettings
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

        var user = await _userManager.FindByNameAsync(userName);
        if (user is null) return NotFound();

        var existing = await _dbContext.UserSettings
            .Where(s => s.UserId == user.Id)
            .ToListAsync();

        _dbContext.UserSettings.RemoveRange(existing);

        if (request.Settings is not null)
        {
            foreach (var kv in request.Settings)
            {
                _dbContext.UserSettings.Add(new Domain.Users.UserSetting
                {
                    UserId = user.Id,
                    Key = kv.Key,
                    Value = kv.Value
                });
            }
        }

        await _dbContext.SaveChangesAsync();
        return NoContent();
    }
}
