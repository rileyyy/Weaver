using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

[TestFixture]
public class QuickPicksControllerTests
{
    [Test]
    public async Task List_ReturnsEachPickWithItsDates()
    {
        var service = new Mock<IQuickPickService>(MockBehavior.Strict);
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new ResolvedQuickPick(QuickPick.ThisWeekend, new DateRange(new(2026, 11, 7), new(2026, 11, 8))),
        ]);
        var controller = new QuickPicksController(service.Object);

        var result = await controller.List();

        Assert.That(
            (result.Result as OkObjectResult)!.Value,
            Is.EqualTo(new[] { new QuickPickDto(QuickPick.ThisWeekend, new(2026, 11, 7), new(2026, 11, 8)) }));
    }
}
