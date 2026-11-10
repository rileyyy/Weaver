using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class WorkItemTrashTests
{
    private static readonly Guid Home = Guid.NewGuid();
    private static readonly Status ToDo = TestStatuses.ToDo(Home);
    private static readonly DateOnly Today = new(2026, 11, 4);
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private static WorkItem NewItem() => WorkItem.Create(Home, "Task", null, ToDo, rank: 1.0, Today);

    [Test]
    public void ANewItem_IsNotTrashed()
    {
        var item = NewItem();

        Assert.Multiple(() =>
        {
            Assert.That(item.IsTrashed, Is.False);
            Assert.That(item.TrashedAtUtc, Is.Null);
            Assert.That(item.TrashRootId, Is.Null);
        });
    }

    [Test]
    public void MoveToTrash_RecordsWhenAndWhichDeleteTrashedIt()
    {
        var root = NewItem();
        var child = WorkItem.CreateChildOf(root, "Child", null, ToDo, rank: 1.0, Today);

        root.MoveToTrash(root.Id, Now);
        child.MoveToTrash(root.Id, Now);

        Assert.Multiple(() =>
        {
            Assert.That(root.IsTrashed, Is.True);
            Assert.That(root.IsTrashRoot, Is.True);
            Assert.That(child.TrashedAtUtc, Is.EqualTo(Now));
            Assert.That(child.TrashRootId, Is.EqualTo(root.Id));
            Assert.That(child.IsTrashRoot, Is.False);
        });
    }

    [Test]
    public void MoveToTrash_WhenAlreadyTrashed_IsRejected()
    {
        var item = NewItem();
        item.MoveToTrash(item.Id, Now);

        Assert.Throws<DomainValidationException>(() => item.MoveToTrash(item.Id, Now.AddHours(1)));
    }

    [Test]
    public void RestoreFromTrash_ClearsTheTrashState()
    {
        var item = NewItem();
        item.MoveToTrash(item.Id, Now);

        item.RestoreFromTrash();

        Assert.Multiple(() =>
        {
            Assert.That(item.IsTrashed, Is.False);
            Assert.That(item.TrashedAtUtc, Is.Null);
            Assert.That(item.TrashRootId, Is.Null);
        });
    }

    [Test]
    public void RestoreFromTrash_WhenNotTrashed_IsRejected()
    {
        Assert.Throws<DomainValidationException>(() => NewItem().RestoreFromTrash());
    }
}
