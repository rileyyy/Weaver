using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>Anyone signed in can read the server settings; changing one needs an admin (403 otherwise).</summary>
[ApiController]
[Route("api/server-settings")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public class ServerSettingsController : ControllerBase
{
    private readonly IServerSettingsService _settings;

    public ServerSettingsController(IServerSettingsService settings)
    {
        _settings = settings;
    }

    [HttpGet]
    public async Task<ActionResult<ServerSettingsDto>> Get() =>
        Ok(ServerSettingsDto.FromEntity(await _settings.GetAsync()));

    [HttpPut("time-zone")]
    public async Task<ActionResult<ServerSettingsDto>> SetTimeZone(SetTimeZoneRequest request) =>
        Ok(ServerSettingsDto.FromEntity(await _settings.SetTimeZoneAsync(User.GetUserId(), request.TimeZoneId)));
}
