using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>The caller's own mentions in comments; there is no way to read anyone else's.</summary>
[ApiController]
[Route("api/mentions")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class MentionsController(IMentionService mentions) : ControllerBase
{
    private readonly IMentionService _mentions = mentions;

    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<PendingMentionDto>>> ListPending()
    {
        var pending = await _mentions.ListPendingAsync();
        return Ok(pending.Select(PendingMentionDto.FromView));
    }
}
