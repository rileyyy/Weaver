using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class WorkItemTests
{
    private static readonly Guid Home = Guid.NewGuid();
    private static readonly Guid Village = Guid.NewGuid();

    private static readonly Status ToDo = TestStatuses.ToDo(Home);
    private static readonly Status Doing = TestStatuses.Doing(Home);
    private static readonly Status Done = TestStatuses.Done(Home);

    private static readonly DateOnly Today = new(2026, 11, 10);

    private static WorkItem NewItem(Guid? spaceId = null) => WorkItem.Create(spaceId ?? Home, "Task", null, ToDo, rank: 1.0, Today);

    private static IEnumerable<string> Fields(IEnumerable<FieldChange> changes) => changes.Select(c => c.Field);

    [TestCase("")]
    [TestCase("   ")]
    public void Create_WithABlankTitle_IsRejected(string title)
    {
        Assert.Throws<DomainValidationException>(() => WorkItem.Create(Home, title, null, ToDo, 1.0, Today));
    }

    [Test]
    public void Create_WithATooLongTitle_IsRejected()
    {
        var title = new string('x', WorkItem.TitleMaxLength + 1);

        Assert.Throws<DomainValidationException>(() => WorkItem.Create(Home, title, null, ToDo, 1.0, Today));
    }

    [Test]
    public void MoveToStatus_NeverChangesTheParent()
    {
        var parent = NewItem();
        var parentId = parent.Id;
        var item = WorkItem.CreateChildOf(parent, "Task", null, ToDo, rank: 1.0, Today);

        item.MoveToStatus(Done, rank: 5.0, Today);

        Assert.Multiple(() =>
        {
            Assert.That(item.StatusId, Is.EqualTo(Done.Id));
            Assert.That(item.Rank, Is.EqualTo(5.0));
            Assert.That(item.ParentId, Is.EqualTo(parentId));
        });
    }

    [Test]
    public void MoveToParent_KeepsTheStatus()
    {
        var item = NewItem();
        var newParent = NewItem();

        item.MoveToParent(newParent, rank: 2.0);

        Assert.Multiple(() =>
        {
            Assert.That(item.ParentId, Is.EqualTo(newParent.Id));
            Assert.That(item.StatusId, Is.EqualTo(ToDo.Id));
        });
    }

    [Test]
    public void MoveToParent_ItsOwnId_IsRejected()
    {
        var item = NewItem();

        Assert.Throws<CyclicParentException>(() => item.MoveToParent(item, rank: 1.0));
    }

    [Test]
    public void Create_PutsATopLevelItemInTheChosenSpace()
    {
        var item = WorkItem.Create(Village, "Fix the fence", null, ToDo, rank: 1.0, Today);

        Assert.Multiple(() =>
        {
            Assert.That(item.SpaceId, Is.EqualTo(Village));
            Assert.That(item.ParentId, Is.Null);
        });
    }

    [Test]
    public void CreateChildOf_InheritsTheParentsSpace()
    {
        var parent = NewItem(Village);

        var child = WorkItem.CreateChildOf(parent, "Paint it", null, ToDo, rank: 1.0, Today);

        Assert.Multiple(() =>
        {
            Assert.That(child.SpaceId, Is.EqualTo(Village));
            Assert.That(child.ParentId, Is.EqualTo(parent.Id));
        });
    }

    [Test]
    public void MoveToParent_UnderAnItemInAnotherSpace_IsRejected()
    {
        var item = NewItem(Home);
        var elsewhere = NewItem(Village);

        Assert.Throws<CrossSpaceParentException>(() => item.MoveToParent(elsewhere, rank: 1.0));
        Assert.Multiple(() =>
        {
            Assert.That(item.ParentId, Is.Null);
            Assert.That(item.SpaceId, Is.EqualTo(Home));
        });
    }

    [Test]
    public void MoveToParent_ToTopLevel_StaysInItsSpace()
    {
        var parent = NewItem(Village);
        var child = WorkItem.CreateChildOf(parent, "Paint it", null, ToDo, rank: 1.0, Today);

        var changed = child.MoveToParent(null, rank: 1.0);

        Assert.Multiple(() =>
        {
            Assert.That(child.ParentId, Is.Null);
            Assert.That(child.SpaceId, Is.EqualTo(Village));
            Assert.That(Fields(changed), Does.Not.Contain(WorkItemField.SpaceId));
        });
    }

    [Test]
    public void CopyForOccurrence_StaysInTheTemplatesSpace()
    {
        var template = NewItem(Village);

        var copy = template.CopyForOccurrence(null, ToDo, 1.0, new DateOnly(2026, 11, 7), Today);

        Assert.That(copy.SpaceId, Is.EqualTo(Village));
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
        var typeId = Guid.NewGuid();
        var priorityId = Guid.NewGuid();
        var item = WorkItem.Create(Home, "Task", "Keep me", ToDo, 1.0, Today, typeId, priorityId);

        var changed = item.UpdateDetails(new WorkItemDetailsPatch { Title = "Renamed" });

        Assert.Multiple(() =>
        {
            Assert.That(item.Title, Is.EqualTo("Renamed"));
            Assert.That(item.Description, Is.EqualTo("Keep me"));
            Assert.That(item.TypeId, Is.EqualTo(typeId));
            Assert.That(item.PriorityId, Is.EqualTo(priorityId));
            Assert.That(Fields(changed), Is.EqualTo(new[] { WorkItemField.Title }));
        });
    }

    [Test]
    public void UpdateDetails_WithExplicitNulls_ClearsThoseFields()
    {
        var item = WorkItem.Create(Home, "Task", "Clear me", ToDo, 1.0, Today, Guid.NewGuid(), Guid.NewGuid());

        var changed = item.UpdateDetails(new WorkItemDetailsPatch { Description = null, TypeId = null, PriorityId = null });

        Assert.Multiple(() =>
        {
            Assert.That(item.Title, Is.EqualTo("Task"));
            Assert.That(item.Description, Is.Null);
            Assert.That(item.TypeId, Is.Null);
            Assert.That(item.PriorityId, Is.Null);
            Assert.That(Fields(changed), Is.EqualTo(new[] { WorkItemField.Description, WorkItemField.TypeId, WorkItemField.PriorityId }));
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
    public void UpdateDetails_WithAWhen_StoresItTrimmedAndReportsIt()
    {
        var item = NewItem();

        var changed = item.UpdateDetails(new WorkItemDetailsPatch { When = "  before the Christmas market " });

        Assert.Multiple(() =>
        {
            Assert.That(item.When, Is.EqualTo("before the Christmas market"));
            Assert.That(Fields(changed), Is.EqualTo(new[] { WorkItemField.When }));
        });
    }

    [TestCase(null)]
    [TestCase("   ")]
    public void UpdateDetails_WithANullOrBlankWhen_ClearsIt(string? when)
    {
        var item = NewItem();
        item.UpdateDetails(new WorkItemDetailsPatch { When = "after drywall" });

        item.UpdateDetails(new WorkItemDetailsPatch { When = when });

        Assert.That(item.When, Is.Null);
    }

    [Test]
    public void UpdateDetails_WithATooLongWhen_IsRejected()
    {
        var item = NewItem();
        var when = new string('x', WorkItem.WhenMaxLength + 1);

        Assert.Throws<DomainValidationException>(() => item.UpdateDetails(new WorkItemDetailsPatch { When = when }));
    }

    [Test]
    public void UpdateDetails_LeavingWhenOut_KeepsIt()
    {
        var item = NewItem();
        item.UpdateDetails(new WorkItemDetailsPatch { When = "next spring" });

        item.UpdateDetails(new WorkItemDetailsPatch { Title = "Renamed" });

        Assert.That(item.When, Is.EqualTo("next spring"));
    }

    [Test]
    public void IsWhenResolved_WithAWhenButNoDates_IsFalse()
    {
        var item = NewItem();

        item.UpdateDetails(new WorkItemDetailsPatch { When = "after drywall" });

        Assert.That(item.IsWhenResolved(hasLiveBlocker: false), Is.False);
    }

    [Test]
    public void IsWhenResolved_WithAWhenAndALiveBlocker_IsTrue()
    {
        var item = NewItem();

        item.UpdateDetails(new WorkItemDetailsPatch { When = "after drywall" });

        Assert.That(item.IsWhenResolved(hasLiveBlocker: true), Is.True);
    }

    [Test]
    public void WhenResolutionNeedsBlockers_OnlyForAWhenWithoutDates()
    {
        var undated = NewItem();
        undated.UpdateDetails(new WorkItemDetailsPatch { When = "after drywall" });
        var dated = NewItem();
        dated.UpdateDetails(new WorkItemDetailsPatch { When = "next week" });
        dated.Reschedule(new DateOnly(2026, 11, 9), null);

        Assert.Multiple(() =>
        {
            Assert.That(undated.WhenResolutionNeedsBlockers, Is.True);
            Assert.That(dated.WhenResolutionNeedsBlockers, Is.False);
            Assert.That(NewItem().WhenResolutionNeedsBlockers, Is.False);
        });
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void IsWhenResolved_WithAWhenAndAnyDate_IsTrue(bool hasStart, bool hasEnd)
    {
        var item = NewItem();
        item.UpdateDetails(new WorkItemDetailsPatch { When = "next week" });

        item.Reschedule(hasStart ? new DateOnly(2026, 11, 9) : null, hasEnd ? new DateOnly(2026, 11, 15) : null);

        Assert.That(item.IsWhenResolved(hasLiveBlocker: false), Is.True);
    }

    [Test]
    public void IsWhenResolved_WithDatesButNoWhen_IsFalse_BecauseThereIsNothingToResolve()
    {
        var item = NewItem();

        item.Reschedule(new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 15));

        Assert.Multiple(() =>
        {
            Assert.That(item.IsWhenResolved(hasLiveBlocker: false), Is.False);
            Assert.That(item.IsWhenResolved(hasLiveBlocker: true), Is.False);
        });
    }

    [Test]
    public void CopyForOccurrence_KeepsTheWhen()
    {
        var item = NewItem();
        item.UpdateDetails(new WorkItemDetailsPatch { When = "every market day" });

        var copy = item.CopyForOccurrence(null, ToDo, 1.0, new DateOnly(2026, 11, 7), Today);

        Assert.That(copy.When, Is.EqualTo("every market day"));
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
            Assert.That(Fields(changed), Is.EqualTo(new[] { WorkItemField.EndDate }));
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
            Assert.That(Fields(changed), Is.EqualTo(new[] { WorkItemField.StartDate }));
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
            Assert.That(Fields(first), Is.EqualTo(new[] { WorkItemField.Tags }));
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
            Assert.That(Fields(assigned), Is.EqualTo(new[] { WorkItemField.AssignedToUserId }));
            Assert.That(again, Is.Empty);
        });
    }

    [Test]
    public void MoveToStatus_ReportsTheStatusAndRankThatChanged()
    {
        var item = NewItem();

        var moved = item.MoveToStatus(Doing, rank: 1.0, Today);
        var reordered = item.MoveToStatus(Doing, rank: 2.0, Today);

        Assert.Multiple(() =>
        {
            Assert.That(Fields(moved), Is.EqualTo(new[] { WorkItemField.StatusId }));
            Assert.That(Fields(reordered), Is.EqualTo(new[] { WorkItemField.Rank }));
        });
    }

    [Test]
    public void MoveToParent_ReportsTheParentAndRankThatChanged()
    {
        var item = NewItem();

        var changed = item.MoveToParent(NewItem(), rank: 3.0);

        Assert.That(Fields(changed), Is.EqualTo(new[] { WorkItemField.ParentId, WorkItemField.Rank }));
    }

    [Test]
    public void UpdateDetails_ReportsEachChangedFieldsOldAndNewValue()
    {
        var item = WorkItem.Create(Home, "Task", "Old notes", ToDo, 1.0, Today);

        var changed = item.UpdateDetails(new WorkItemDetailsPatch { Title = "Renamed", Description = null });

        Assert.That(changed, Is.EqualTo(new[]
        {
            new FieldChange(WorkItemField.Title, "Task", "Renamed"),
            new FieldChange(WorkItemField.Description, "Old notes", null),
        }));
    }

    [Test]
    public void SetTags_ReportsTheOldAndNewTagLists()
    {
        var item = NewItem();
        item.SetTags(["urgent"]);

        var change = item.SetTags(["urgent", "later"]).Single();

        Assert.Multiple(() =>
        {
            Assert.That(change.Field, Is.EqualTo(WorkItemField.Tags));
            Assert.That(change.OldValue, Is.EqualTo(new[] { "urgent" }));
            Assert.That(change.NewValue, Is.EqualTo(new[] { "urgent", "later" }));
        });
    }
}
