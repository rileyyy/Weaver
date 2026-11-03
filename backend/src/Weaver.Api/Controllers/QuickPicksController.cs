using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>
/// The When quick picks with their dates for today in the server's time zone. Applying one is
/// an ordinary <c>PATCH /work-items/{id}/schedule</c> with these dates.
/// </summary>
[ApiController]
[Route("api/quick-picks")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class QuickPicksController : ControllerBase
{
    private readonly IQuickPickService _quickPicks;

    public QuickPicksController(IQuickPickService quickPicks)
    {
        _quickPicks = quickPicks;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<QuickPickDto>>> List() =>
        Ok((await _quickPicks.ListAsync()).Select(QuickPickDto.FromResolved).ToList());
}
