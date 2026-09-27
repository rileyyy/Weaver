using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class WorkItemTests
{
    private static readonly Guid ToDo = Guid.NewGuid();
    private static readonly Guid Done = Guid.NewGuid();

    private static WorkItem NewItem(Guid? parentId = null) => WorkItem.Create("Task", null, parentId, ToDo, rank: 1.0);

    [TestCase("")]
    [TestCase("   ")]
    public void Create_WithABlankTitle_IsRejected(string title)
    {
        Assert.Throws<DomainValidationException>(() => WorkItem.Create(title, null, null, ToDo, 1.0));
    }

    [Test]
    public void Create_WithATooLongTitle_IsRejected()
    {
        var title = new string('x', WorkItem.TitleMaxLength + 1);

        Assert.Throws<DomainValidationException>(() => WorkItem.Create(title, null, null, ToDo, 1.0));
    }

    [Test]
    public void MoveToStatus_NeverChangesTheParent()
    {
        var parentId = Guid.NewGuid();
        var item = NewItem(parentId);

        item.MoveToStatus(Done, rank: 5.0);

        Assert.Multiple(() =>
        {
            Assert.That(item.StatusId, Is.EqualTo(Done));
            Assert.That(item.Rank, Is.EqualTo(5.0));
            Assert.That(item.ParentId, Is.EqualTo(parentId));
        });
    }

    [Test]
    public void MoveToParent_KeepsTheStatus()
    {
        var item = NewItem();
        var newParent = Guid.NewGuid();

        item.MoveToParent(newParent, rank: 2.0);

        Assert.Multiple(() =>
        {
            Assert.That(item.ParentId, Is.EqualTo(newParent));
            Assert.That(item.StatusId, Is.EqualTo(ToDo));
        });
    }

    [Test]
    public void MoveToParent_ItsOwnId_IsRejected()
    {
        var item = NewItem();

        Assert.Throws<CyclicParentException>(() => item.MoveToParent(item.Id, rank: 1.0));
    }

    [Test]
    public void Reschedule_WithStartAfterEnd_IsRejected()
    {
        var item = NewItem();

        Assert.Throws<InvalidWorkItemScheduleException>(() =>
            item.Reschedule(new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 24)));
    }

    [Test]
    public void Reschedule_AllowsAnOpenStartOrEnd()
    {
        var item = NewItem();

        item.Reschedule(null, new DateOnly(2026, 9, 24));

        Assert.That(item.EndDate, Is.EqualTo(new DateOnly(2026, 9, 24)));
    }

    [Test]
    public void SetTags_TrimsAndDeDuplicatesCaseInsensitively_KeepingTheFirstSpelling()
    {
        var item = NewItem();

        item.SetTags([" Urgent ", "urgent", "needs review"]);

        Assert.That(item.Tags, Is.EqualTo(new[] { "Urgent", "needs review" }));
    }

    [Test]
    public void SetTags_WithABlankTag_IsRejected()
    {
        var item = NewItem();

        Assert.Throws<InvalidWorkItemTagException>(() => item.SetTags(["ok", "  "]));
    }

    [Test]
    public void SetTags_WithTooManyTags_IsRejected()
    {
        var item = NewItem();
        var tags = Enumerable.Range(0, WorkItem.MaxTags + 1).Select(i => $"tag{i}");

        Assert.Throws<DomainValidationException>(() => item.SetTags(tags));
    }

    [Test]
    public void SetTags_WithATooLongTag_IsRejected()
    {
        var item = NewItem();

        Assert.Throws<DomainValidationException>(() => item.SetTags([new string('x', WorkItem.TagMaxLength + 1)]));
    }
}
