using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

[TestFixture]
public class WorkItemSearchControllerTests
{
    [Test]
    public async Task Search_PassesEveryInputThrough_AndReturnsFullItems()
    {
        var spaceId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var since = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
        var parameters = new WorkItemSearchParameters
        {
            SpaceIds = [spaceId],
            Text = "paint",
            Number = 12,
            StatusCategories = [StatusCategory.Doing],
            AssigneeId = assigneeId,
            Tags = ["outside"],
            TagMatch = TagMatch.All,
            TypeIds = [typeId],
            From = new DateOnly(2026, 11, 1),
            To = new DateOnly(2026, 11, 30),
            BacklogOnly = true,
            UpdatedSince = since,
            Cursor = "abc",
            Limit = 5,
        };
        WorkItemQuery? received = null;
        var service = new Mock<IWorkItemQueryService>(MockBehavior.Strict);
        service
            .Setup(s => s.QueryAsync(It.IsAny<WorkItemQuery>(), It.IsAny<CancellationToken>()))
            .Callback<WorkItemQuery, CancellationToken>((q, _) => received = q)
            .ReturnsAsync(new WorkItemQueryPage<WorkItem>([], "next"));

        var result = await new WorkItemSearchController(service.Object, new WorkItemDtoMapper(new FakeWorkItemBlockers())).Search(parameters);

        var page = (WorkItemPageDto)((OkObjectResult)result.Result!).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(page.NextCursor, Is.EqualTo("next"));
            Assert.That(received!.SpaceIds, Is.EqualTo(new[] { spaceId }));
            Assert.That(received.Text, Is.EqualTo("paint"));
            Assert.That(received.Number, Is.EqualTo(12));
            Assert.That(received.StatusCategories, Is.EqualTo(new[] { StatusCategory.Doing }));
            Assert.That(received.AssigneeId, Is.EqualTo(assigneeId));
            Assert.That(received.Tags, Is.EqualTo(new[] { "outside" }));
            Assert.That(received.TagMatch, Is.EqualTo(TagMatch.All));
            Assert.That(received.TypeIds, Is.EqualTo(new[] { typeId }));
            Assert.That(received.From, Is.EqualTo(new DateOnly(2026, 11, 1)));
            Assert.That(received.To, Is.EqualTo(new DateOnly(2026, 11, 30)));
            Assert.That(received.BacklogOnly, Is.True);
            Assert.That(received.UpdatedSince, Is.EqualTo(since));
            Assert.That(received.Cursor, Is.EqualTo("abc"));
            Assert.That(received.Limit, Is.EqualTo(5));
        });
    }

    [Test]
    public void NoParameters_IsTheUnfilteredFirstPage()
    {
        Assert.That(new WorkItemSearchParameters().ToQuery(), Is.EqualTo(new WorkItemQuery()));
    }
}
