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

    [Test]
    public void UpdateDetails_WithOnlyATitle_LeavesTheOtherFieldsAlone()
    {
        var layerId = Guid.NewGuid();
        var priorityId = Guid.NewGuid();
        var item = WorkItem.Create("Task", "Keep me", null, ToDo, 1.0, layerId, priorityId);

        var changed = item.UpdateDetails(new WorkItemDetailsPatch { Title = "Renamed" });

        Assert.Multiple(() =>
        {
            Assert.That(item.Title, Is.EqualTo("Renamed"));
            Assert.That(item.Description, Is.EqualTo("Keep me"));
            Assert.That(item.LayerId, Is.EqualTo(layerId));
            Assert.That(item.PriorityId, Is.EqualTo(priorityId));
            Assert.That(changed, Is.EqualTo(new[] { WorkItemField.Title }));
        });
    }

    [Test]
    public void UpdateDetails_WithExplicitNulls_ClearsThoseFields()
    {
        var item = WorkItem.Create("Task", "Clear me", null, ToDo, 1.0, Guid.NewGuid(), Guid.NewGuid());

        var changed = item.UpdateDetails(new WorkItemDetailsPatch { Description = null, LayerId = null, PriorityId = null });

        Assert.Multiple(() =>
        {
            Assert.That(item.Title, Is.EqualTo("Task"));
            Assert.That(item.Description, Is.Null);
            Assert.That(item.LayerId, Is.Null);
            Assert.That(item.PriorityId, Is.Null);
            Assert.That(changed, Is.EqualTo(new[] { WorkItemField.Description, WorkItemField.LayerId, WorkItemField.PriorityId }));
        });
    }

    [Test]
    public void UpdateDetails_ToTheCurrentValues_ReportsNothingChanged()
    {
        var item = NewItem();

        var changed = item.UpdateDetails(new WorkItemDetailsPatch { Title = "Task", Description = null });

        Assert.That(changed, Is.Empty);
    }

    [Test]
    public void UpdateDetails_ClearingTheTitle_IsRejected()
    {
        var item = NewItem();

        Assert.Throws<DomainValidationException>(() => item.UpdateDetails(new WorkItemDetailsPatch { Title = null! }));
    }

    [Test]
    public void Reschedule_WithOnlyAnEndDate_KeepsTheStartDate()
    {
        var item = NewItem();
        item.Reschedule(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 5));

        var changed = item.Reschedule(Optional<DateOnly?>.Unset, new DateOnly(2026, 9, 10));

        Assert.Multiple(() =>
        {
            Assert.That(item.StartDate, Is.EqualTo(new DateOnly(2026, 9, 1)));
            Assert.That(item.EndDate, Is.EqualTo(new DateOnly(2026, 9, 10)));
            Assert.That(changed, Is.EqualTo(new[] { WorkItemField.EndDate }));
        });
    }

    [Test]
    public void Reschedule_WithANullStartDate_ClearsOnlyTheStart()
    {
        var item = NewItem();
        item.Reschedule(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 5));

        var changed = item.Reschedule(null, Optional<DateOnly?>.Unset);

        Assert.Multiple(() =>
        {
            Assert.That(item.StartDate, Is.Null);
            Assert.That(item.EndDate, Is.EqualTo(new DateOnly(2026, 9, 5)));
            Assert.That(changed, Is.EqualTo(new[] { WorkItemField.StartDate }));
        });
    }

    [Test]
    public void Reschedule_AnEndBeforeTheKeptStart_IsRejected()
    {
        var item = NewItem();
        item.Reschedule(new DateOnly(2026, 9, 10), null);

        Assert.Throws<InvalidWorkItemScheduleException>(() =>
            item.Reschedule(Optional<DateOnly?>.Unset, new DateOnly(2026, 9, 1)));
    }

    [Test]
    public void SetTags_ReportsTagsChangedOnlyWhenTheNormalizedListDiffers()
    {
        var item = NewItem();

        var first = item.SetTags(["urgent"]);
        var same = item.SetTags([" urgent "]);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(new[] { WorkItemField.Tags }));
            Assert.That(same, Is.Empty);
        });
    }

    [Test]
    public void AssignTo_ReportsTheAssigneeChangedOnlyWhenItDiffers()
    {
        var item = NewItem();
        var userId = Guid.NewGuid();

        var assigned = item.AssignTo(userId);
        var again = item.AssignTo(userId);

        Assert.Multiple(() =>
        {
            Assert.That(assigned, Is.EqualTo(new[] { WorkItemField.AssignedToUserId }));
            Assert.That(again, Is.Empty);
        });
    }

    [Test]
    public void MoveToStatus_ReportsTheStatusAndRankThatChanged()
    {
        var item = NewItem();

        var moved = item.MoveToStatus(Done, rank: 1.0);
        var reordered = item.MoveToStatus(Done, rank: 2.0);

        Assert.Multiple(() =>
        {
            Assert.That(moved, Is.EqualTo(new[] { WorkItemField.StatusId }));
            Assert.That(reordered, Is.EqualTo(new[] { WorkItemField.Rank }));
        });
    }

    [Test]
    public void MoveToParent_ReportsTheParentAndRankThatChanged()
    {
        var item = NewItem();

        var changed = item.MoveToParent(Guid.NewGuid(), rank: 3.0);

        Assert.That(changed, Is.EqualTo(new[] { WorkItemField.ParentId, WorkItemField.Rank }));
    }
}
