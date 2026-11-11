using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;

namespace Weaver.Api.Tests;

[TestFixture]
public class AdminRunsControllerTests
{
    private static readonly DateTimeOffset Night = new(2026, 11, 10, 2, 0, 0, TimeSpan.Zero);

    private Mock<IRunReportService> _runs = null!;
    private AdminRunsController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _runs = new Mock<IRunReportService>(MockBehavior.Strict);
        _controller = new AdminRunsController(_runs.Object);
    }

    [Test]
    public async Task List_PassesThePage_AndMapsEachRun()
    {
        var run = new RunReport(Guid.NewGuid(), "Runner: When", Guid.NewGuid(), "nightly", "run-1", Night, Night.AddMinutes(4), 7);
        _runs.Setup(s => s.ListAsync(40, 20, It.IsAny<CancellationToken>())).ReturnsAsync(new RunReportPage([run], 60));

        var result = await _controller.List(offset: 40, limit: 20);

        var page = (RunReportPageDto)((OkObjectResult)result.Result!).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(page.NextOffset, Is.EqualTo(60));
            Assert.That(page.Runs.Single(), Is.EqualTo(new RunReportDto(
                run.AccountId, "Runner: When", run.ApiKeyId, "nightly", "run-1", Night, Night.AddMinutes(4), 7)));
        });
    }

    [Test]
    public async Task ListChanges_AsksForTheRunNamedByItsAccountKeyAndId()
    {
        var key = new RunKey(Guid.NewGuid(), Guid.NewGuid(), "run-1");
        var item = new WorkItemSummary(Guid.NewGuid(), 12, "Paint the hall", Guid.NewGuid(), "Home", null, "To Do", null, null, null, null, 0);
        _runs.Setup(s => s.ListChangesAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ChangedWorkItem(item, Deleted: false, Restored: false, [])]);

        var result = await _controller.ListChanges(key.AccountId, key.ApiKeyId, key.RunId);

        var changed = ((IEnumerable<ChangedWorkItemDto>)((OkObjectResult)result.Result!).Value!).Single();
        Assert.That((changed.Item.Number, changed.Item.Title), Is.EqualTo(((int?)12, "Paint the hall")));
    }
}
