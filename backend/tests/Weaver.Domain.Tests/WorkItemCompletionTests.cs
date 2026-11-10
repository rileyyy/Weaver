using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

/// <summary>The completion date: set on entering a Done status, kept between Done statuses, cleared on leaving.</summary>
[TestFixture]
public class WorkItemCompletionTests
{
    private static readonly Guid Home = Guid.NewGuid();
    private static readonly Guid Village = Guid.NewGuid();

    private static readonly Status ToDo = TestStatuses.ToDo(Home);
    private static readonly Status Doing = TestStatuses.Doing(Home);
    private static readonly Status Done = TestStatuses.Done(Home);
    private static readonly Status Archived = Status.Create(Home, "Archived", 3, StatusCategory.Done, "#616161");

    private static readonly Status VillageOpen = TestStatuses.ToDo(Village);
    private static readonly Status VillageDone = TestStatuses.Done(Village);

    private static readonly DateOnly Monday = new(2026, 11, 9);
    private static readonly DateOnly Friday = new(2026, 11, 13);

    private static WorkItem NewItem(Status? status = null) =>
        WorkItem.Create(Home, "Task", null, status ?? ToDo, rank: 1.0, Monday);

    private static WorkItem DoneOnMonday()
    {
        var item = NewItem();
        item.MoveToStatus(Done, rank: 1.0, Monday);
        return item;
    }

    [Test]
    public void ANewItemInAnOpenStatus_HasNoCompletionDate()
    {
        Assert.That(NewItem().CompletionDate, Is.Null);
    }

    [Test]
    public void ANewItemInADoneStatus_IsCompletedToday()
    {
        Assert.That(NewItem(Done).CompletionDate, Is.EqualTo(Monday));
    }

    [Test]
    public void ANewChildInADoneStatus_IsCompletedToday()
    {
        var child = WorkItem.CreateChildOf(NewItem(), "Child", null, Done, rank: 1.0, Monday);

        Assert.That(child.CompletionDate, Is.EqualTo(Monday));
    }

    [Test]
    public void MoveToStatus_IntoDone_SetsTodayAndReportsIt()
    {
        var item = NewItem();

        var changes = item.MoveToStatus(Done, rank: 1.0, Monday);

        Assert.Multiple(() =>
        {
            Assert.That(item.CompletionDate, Is.EqualTo(Monday));
            Assert.That(changes, Does.Contain(new FieldChange(WorkItemField.CompletionDate, null, Monday)));
        });
    }

    [Test]
    public void MoveToStatus_OutOfDone_ClearsTheDateAndReportsIt()
    {
        var item = DoneOnMonday();

        var changes = item.MoveToStatus(Doing, rank: 1.0, Friday);

        Assert.Multiple(() =>
        {
            Assert.That(item.CompletionDate, Is.Null);
            Assert.That(changes, Does.Contain(new FieldChange(WorkItemField.CompletionDate, Monday, null)));
        });
    }

    [Test]
    public void MoveToStatus_BetweenTwoDoneStatuses_KeepsTheOriginalDate()
    {
        var item = DoneOnMonday();

        var changes = item.MoveToStatus(Archived, rank: 1.0, Friday);

        Assert.Multiple(() =>
        {
            Assert.That(item.CompletionDate, Is.EqualTo(Monday));
            Assert.That(changes.Select(c => c.Field), Does.Not.Contain(WorkItemField.CompletionDate));
        });
    }

    [Test]
    public void MoveToStatus_BetweenTwoOpenStatuses_ReportsNoCompletionChange()
    {
        var item = NewItem();

        var changes = item.MoveToStatus(Doing, rank: 1.0, Friday);

        Assert.That(changes.Select(c => c.Field), Does.Not.Contain(WorkItemField.CompletionDate));
    }

    [Test]
    public void ReopeningAndFinishingAgain_TakesTheNewDay()
    {
        var item = DoneOnMonday();
        item.MoveToStatus(Doing, rank: 1.0, Monday);

        item.MoveToStatus(Done, rank: 1.0, Friday);

        Assert.That(item.CompletionDate, Is.EqualTo(Friday));
    }

    [Test]
    public void MoveToSpace_IntoADoneStatus_SetsToday()
    {
        var item = NewItem();

        var changes = item.MoveToSpace(Village, parent: null, rank: 1.0, new SpaceMoveValues(VillageDone, null, null), Friday);

        Assert.Multiple(() =>
        {
            Assert.That(item.CompletionDate, Is.EqualTo(Friday));
            Assert.That(changes.Select(c => c.Field), Does.Contain(WorkItemField.CompletionDate));
        });
    }

    [Test]
    public void MoveToSpace_FromDoneIntoADoneStatus_KeepsTheOriginalDate()
    {
        var item = DoneOnMonday();

        item.MoveToSpace(Village, parent: null, rank: 1.0, new SpaceMoveValues(VillageDone, null, null), Friday);

        Assert.That(item.CompletionDate, Is.EqualTo(Monday));
    }

    [Test]
    public void MoveToSpace_FromDoneIntoAnOpenStatus_ClearsTheDate()
    {
        var item = DoneOnMonday();

        item.MoveToSpace(Village, parent: null, rank: 1.0, new SpaceMoveValues(VillageOpen, null, null), Friday);

        Assert.That(item.CompletionDate, Is.Null);
    }

    [Test]
    public void FollowParentToSpace_AppliesTheRuleForTheChildsNewStatus()
    {
        var parent = NewItem();
        var child = WorkItem.CreateChildOf(parent, "Child", null, Done, rank: 1.0, Monday);
        parent.MoveToSpace(Village, parent: null, rank: 1.0, new SpaceMoveValues(VillageOpen, null, null), Friday);

        var changes = child.FollowParentToSpace(parent, new SpaceMoveValues(VillageOpen, null, null), Friday);

        Assert.Multiple(() =>
        {
            Assert.That(child.CompletionDate, Is.Null);
            Assert.That(changes, Does.Contain(new FieldChange(WorkItemField.CompletionDate, Monday, null)));
        });
    }

    [Test]
    public void CopyForOccurrence_DoesNotInheritTheCompletionDate()
    {
        var template = DoneOnMonday();

        var copy = template.CopyForOccurrence(null, ToDo, 1.0, Friday, Friday);

        Assert.That(copy.CompletionDate, Is.Null);
    }

    [Test]
    public void CopyForOccurrence_IntoADoneStatus_IsCompletedToday_NotOnTheTemplatesDate()
    {
        var template = DoneOnMonday();

        var copy = template.CopyForOccurrence(null, Done, 1.0, new DateOnly(2026, 11, 20), Friday);

        Assert.That(copy.CompletionDate, Is.EqualTo(Friday));
    }

    [Test]
    public void FollowStatusRecategorized_IntoDone_SetsToday()
    {
        var status = Status.Create(Home, "Shipped", 3, StatusCategory.Doing, "#43A047");
        var item = NewItem(status);
        status.Recategorize(StatusCategory.Done, status.Color);

        var changes = item.FollowStatusRecategorized(status, Friday);

        Assert.Multiple(() =>
        {
            Assert.That(item.CompletionDate, Is.EqualTo(Friday));
            Assert.That(changes, Is.EqualTo(new[] { new FieldChange(WorkItemField.CompletionDate, null, Friday) }));
        });
    }

    [Test]
    public void FollowStatusRecategorized_OutOfDone_ClearsTheDate()
    {
        var status = Status.Create(Home, "Shipped", 3, StatusCategory.Done, "#43A047");
        var item = NewItem(status);
        status.Recategorize(StatusCategory.Doing, status.Color);

        var changes = item.FollowStatusRecategorized(status, Friday);

        Assert.Multiple(() =>
        {
            Assert.That(item.CompletionDate, Is.Null);
            Assert.That(changes, Is.EqualTo(new[] { new FieldChange(WorkItemField.CompletionDate, Monday, null) }));
        });
    }

    [Test]
    public void FollowStatusRecategorized_ForAnotherStatus_IsRejected()
    {
        var item = NewItem();

        Assert.Throws<DomainValidationException>(() => item.FollowStatusRecategorized(Done, Friday));
        Assert.That(item.CompletionDate, Is.Null);
    }
}
