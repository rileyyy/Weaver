using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var result = await _auth.RegisterAsync(request.Username, request.Password);
        return Ok(AuthResponse.FromResult(result));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var result = await _auth.LoginAsync(request.Username, request.Password);
        return Ok(AuthResponse.FromResult(result));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
    {
        var result = await _auth.RefreshAsync(request.RefreshToken);
        return Ok(AuthResponse.FromResult(result));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(LogoutRequest request)
    {
        await _auth.LogoutAsync(request.RefreshToken);
        return NoContent();
    }

    /// <summary>
    /// The currently authenticated user, resolved from the access token —
    /// lets a client confirm a cached/refreshed token is still valid and
    /// know who it belongs to without decoding the JWT itself. Relies on
    /// the app-wide fallback policy (see Program.cs) rather than its own
    /// [Authorize], since a controller-level [AllowAnonymous] would
    /// otherwise win over one here — see ASP0026.
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me([FromServices] IUserService users)
    {
        var user = await users.GetByIdAsync(User.GetUserId());
        return user is null ? NotFound() : Ok(UserDto.FromEntity(user));
    }
}
