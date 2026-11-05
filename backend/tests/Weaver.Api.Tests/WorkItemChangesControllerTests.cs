using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;

namespace Weaver.Api.Tests;

[TestFixture]
public class WorkItemChangesControllerTests
{
    [Test]
    public async Task ListChangesSince_PassesTheQueryThrough_AndReturnsThePage()
    {
        var since = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
        var spaceId = Guid.NewGuid();
        var summary = new WorkItemSummary(Guid.NewGuid(), 7, "Boiler", spaceId, "Home", null, "Doing", null, null, null, "alice", 0);
        var service = new Mock<IWorkItemChangesService>(MockBehavior.Strict);
        service
            .Setup(s => s.ListSinceAsync(new WorkItemChangesQuery(since, spaceId, true, "abc", 5), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemChangesPage([new ChangedWorkItem(summary, Deleted: false, Restored: true, [])], null));

        var result = await new WorkItemChangesController(service.Object).ListChangesSince(since, spaceId, true, "abc", 5);

        var page = (WorkItemChangesPageDto)((OkObjectResult)result.Result!).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(page.Items.Single().Item, Is.EqualTo(WorkItemSummaryDto.FromSummary(summary)));
            Assert.That(page.Items.Single().Deleted, Is.False);
            Assert.That(page.Items.Single().Restored, Is.True);
            Assert.That(page.NextCursor, Is.Null);
        });
    }
}
