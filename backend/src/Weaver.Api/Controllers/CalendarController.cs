using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>
/// Today's date in the server's time zone. Clients ask for it rather than reading their own
/// clock, so a phone in another zone (or the app with no time-zone database) still draws
/// "today" where every server rule puts it, e.g. the Agenda's Overdue and Today.
/// </summary>
[ApiController]
[Route("api/calendar")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class CalendarController(IServerCalendar calendar) : ControllerBase
{
    private readonly IServerCalendar _calendar = calendar;

    [HttpGet("today")]
    public async Task<ActionResult<CalendarTodayDto>> Today() =>
        Ok(new CalendarTodayDto(await _calendar.TodayAsync()));
}
