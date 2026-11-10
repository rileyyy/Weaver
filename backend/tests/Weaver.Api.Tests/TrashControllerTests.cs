using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

[TestFixture]
public class TrashControllerTests
{
    private Mock<IWorkItemTrashService> _trash = null!;
    private Mock<IWorkItemPurgeService> _purge = null!;
    private TrashController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _trash = new Mock<IWorkItemTrashService>(MockBehavior.Strict);
        _purge = new Mock<IWorkItemPurgeService>(MockBehavior.Strict);
        _controller = new TrashController(_trash.Object, _purge.Object, new WorkItemDtoMapper(new FakeWorkItemBlockers(), new FakeWorkItemNameLookup()));
    }

    public static TrashedWorkItem MakeTrashed(Guid id) => new(
        new WorkItemSummary(id, 12, "Old shed", Guid.NewGuid(), "Home", null, "To Do", null, null, null, null, 0,
            new DateTimeOffset(2026, 11, 4, 9, 0, 0, TimeSpan.Zero)),
        new DateTimeOffset(2026, 11, 4, 9, 0, 0, TimeSpan.Zero),
        ItemCount: 3,
        DeletedByName: "claude",
        DeletedByKind: UserKind.Agent,
        DeletedByActorLabel: "tidy-up");

    [Test]
    public async Task Delete_MovesToTheTrash_PassingCascadeAndExpectedVersion_AndReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _trash.Setup(s => s.MoveToTrashAsync(id, true, 7u, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.Delete(id, cascade: true, expectedVersion: 7u);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        _trash.VerifyAll();
    }

    [Test]
    public async Task List_MapsEachDelete_ForTheGivenSpace()
    {
        var id = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        _trash.Setup(s => s.ListAsync(spaceId, It.IsAny<CancellationToken>())).ReturnsAsync([MakeTrashed(id)]);

        var result = await _controller.List(spaceId);

        var dto = ((IEnumerable<TrashedWorkItemDto>)((OkObjectResult)result.Result!).Value!).Single();
        Assert.Multiple(() =>
        {
            Assert.That(dto.Item.Id, Is.EqualTo(id));
            Assert.That(dto.Item.TrashedAtUtc, Is.Not.Null);
            Assert.That(dto.ItemCount, Is.EqualTo(3));
            Assert.That(dto.DeletedByName, Is.EqualTo("claude"));
            Assert.That(dto.DeletedByKind, Is.EqualTo(UserKind.Agent));
            Assert.That(dto.DeletedByActorLabel, Is.EqualTo("tidy-up"));
        });
    }

    [Test]
    public async Task Restore_ReturnsTheRestoredItem()
    {
        var item = TestWorkItems.Create("Old shed");
        _trash.Setup(s => s.RestoreAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _controller.Restore(item.Id);

        Assert.That(((WorkItemDto)((OkObjectResult)result.Result!).Value!).Id, Is.EqualTo(item.Id));
    }

    [Test]
    public async Task Purge_PurgesThatDelete_AndReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _purge.Setup(s => s.PurgeAsync(id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.Purge(id);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        _purge.VerifyAll();
    }

    [Test]
    public async Task EmptyTrash_EmptiesTheWholeTrash_AndReturnsNoContent()
    {
        _purge.Setup(s => s.EmptyTrashAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.EmptyTrash();

        Assert.That(result, Is.TypeOf<NoContentResult>());
        _purge.VerifyAll();
    }
}
