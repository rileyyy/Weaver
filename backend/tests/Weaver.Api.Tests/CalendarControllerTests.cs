using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;

namespace Weaver.Api.Tests;

[TestFixture]
public class CalendarControllerTests
{
    [Test]
    public async Task Today_ReturnsTheServersDate()
    {
        var calendar = new Mock<IServerCalendar>(MockBehavior.Strict);
        calendar.Setup(c => c.TodayAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new DateOnly(2026, 11, 11));
        var controller = new CalendarController(calendar.Object);

        var result = await controller.Today();

        Assert.That((result.Result as OkObjectResult)!.Value, Is.EqualTo(new CalendarTodayDto(new DateOnly(2026, 11, 11))));
    }
}
