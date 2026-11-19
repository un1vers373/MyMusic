using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using MyMusic.DTOs;
using MyMusic.Models;
using MyMusic.Services;

namespace MyMusic.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth, IOptions<UploadOptions> upload) : ControllerBase
{
    [HttpGet("config")]
    public async Task<AppConfigDto> Config(CancellationToken ct) =>
        new(await auth.RegistrationOpenAsync(ct), upload.Value.MaxAudioMb, upload.Value.MaxCoverMb);

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest req, CancellationToken ct)
    {
        var user = await auth.RegisterAsync(req, ct);
        await SignInAsync(user);
        return new UserDto(user.Id, user.Username);
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<UserDto>> Login(LoginRequest req, CancellationToken ct)
    {
        var user = await auth.LoginAsync(req, ct);
        await SignInAsync(user);
        return new UserDto(user.Id, user.Username);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var user = await auth.FindAsync(User.GetUserId(), ct);
        if (user == null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Unauthorized(new { error = "Session is no longer valid." });
        }
        return new UserDto(user.Id, user.Username);
    }

    private Task SignInAsync(User user)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Username)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        return HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }
}
