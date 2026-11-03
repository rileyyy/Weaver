using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>
/// Each Space owns its statuses and priorities (ADR-0001): listing, names, order, replacements
/// and the last-value rule apply within one Space, and a work item only takes its own Space's.
/// </summary>
[TestFixture]
public class PerSpaceLookupTests
{
    private WeaverDbContext _db = null!;
    private StatusService _statuses = null!;
    private WorkItemPriorityService _priorities = null!;
    private WorkItemService _workItems = null!;
    private User _admin = null!;
    private TestSpace _home = null!;
    private TestSpace _software = null!;

    [SetUp]
    public void SetUp()
    {
        _db = TestDatabase.Create(new FixedTimeProvider(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero)));
        var admins = new AdminAuthorizer(_db);
        _statuses = new StatusService(_db, admins, FakeCallerContext.Anyone());
        _priorities = new WorkItemPriorityService(_db, admins, FakeCallerContext.Anyone());
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), FakeCallerContext.Anyone());
        _admin = TestUsers.Add(_db, "admin", isAdmin: true);
        _home = TestSpaces.Add(_db, "Home");
        _software = TestSpaces.Add(_db, "Software");
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task GetAllAsync_ListsOnlyThatSpacesStatuses()
    {
        await _statuses.CreateAsync(_admin.Id, _software.Id, "Resolved", StatusCategory.Done, "#43A047");

        var home = await _statuses.GetAllAsync(_home.Id);
        var software = await _statuses.GetAllAsync(_software.Id);

        Assert.Multiple(() =>
        {
            Assert.That(home.Select(s => s.Name), Is.EqualTo(new[] { "To Do", "Doing", "Done" }));
            Assert.That(software.Select(s => s.Name), Is.EqualTo(new[] { "To Do", "Doing", "Done", "Resolved" }));
            Assert.That(software.Last().Order, Is.EqualTo(3));
        });
    }

    [Test]
    public void CreateAsync_InAnUnknownSpace_IsNotFound()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _statuses.CreateAsync(_admin.Id, Guid.NewGuid(), "Quoted", StatusCategory.ToDo, "#1E88E5"));
    }

    [Test]
    public async Task CreateAsync_ANameAnotherSpaceUses_IsAllowed_ButNotOneItsOwnSpaceUses()
    {
        await _statuses.CreateAsync(_admin.Id, _home.Id, "Quoted", StatusCategory.ToDo, "#1E88E5");

        Assert.DoesNotThrowAsync(() =>
            _statuses.CreateAsync(_admin.Id, _software.Id, "Quoted", StatusCategory.ToDo, "#1E88E5"));
        Assert.ThrowsAsync<LookupNameTakenException>(() =>
            _statuses.CreateAsync(_admin.Id, _home.Id, "quoted", StatusCategory.ToDo, "#1E88E5"));
    }

    [Test]
    public void UpdateAsync_ToANameAnotherSpaceUses_IsAllowed()
    {
        Assert.DoesNotThrowAsync(() =>
            _priorities.RenameAsync(_admin.Id, _home.Low, "Someday"));
        Assert.DoesNotThrowAsync(() =>
            _priorities.RenameAsync(_admin.Id, _software.Low, "Someday"));
    }

    [Test]
    public async Task ReorderAsync_ReordersOneSpaceAndLeavesTheOtherAlone()
    {
        await _statuses.ReorderAsync(_admin.Id, _software.Id, [_software.Done, _software.Doing, _software.ToDo]);

        Assert.Multiple(async () =>
        {
            Assert.That((await _statuses.GetAllAsync(_software.Id)).Select(s => s.Name),
                Is.EqualTo(new[] { "Done", "Doing", "To Do" }));
            Assert.That((await _statuses.GetAllAsync(_home.Id)).Select(s => s.Name),
                Is.EqualTo(new[] { "To Do", "Doing", "Done" }));
        });
    }

    [Test]
    public void ReorderAsync_WithAnotherSpacesStatus_IsAValidationError()
    {
        Assert.ThrowsAsync<DomainValidationException>(() =>
            _statuses.ReorderAsync(_admin.Id, _home.Id, [_home.Done, _home.Doing, _software.ToDo]));
    }

    [Test]
    public void DeleteAsync_WithAReplacementFromAnotherSpace_IsNotFound()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _statuses.DeleteAsync(_admin.Id, _home.Doing, _software.Doing));
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _priorities.DeleteAsync(_admin.Id, _home.High, _software.High));
    }

    [Test]
    public async Task DeleteAsync_ASpacesLastStatus_IsAConflictWhileOtherSpacesHaveMore()
    {
        await _statuses.DeleteAsync(_admin.Id, _home.Doing, replacementId: null);
        await _statuses.DeleteAsync(_admin.Id, _home.Done, replacementId: null);

        Assert.ThrowsAsync<LastLookupValueException>(() =>
            _statuses.DeleteAsync(_admin.Id, _home.ToDo, replacementId: null));
    }

    [Test]
    public async Task DeleteAsync_AStatusInUse_NeedsAReplacementEvenIfTheSameNameIsFreeElsewhere()
    {
        await _workItems.CreateAsync("Fence", null, null, _home.Doing, spaceId: _home.Id);

        Assert.ThrowsAsync<LookupValueInUseException>(() =>
            _statuses.DeleteAsync(_admin.Id, _home.Doing, replacementId: null));
        Assert.DoesNotThrowAsync(() =>
            _statuses.DeleteAsync(_admin.Id, _software.Doing, replacementId: null));
    }

    [Test]
    public void CreatingAWorkItemWithAnotherSpacesStatus_IsRejected()
    {
        Assert.ThrowsAsync<LookupNotInSpaceException>(() =>
            _workItems.CreateAsync("Fence", null, null, _software.ToDo, spaceId: _home.Id));
    }

    [Test]
    public void CreatingAWorkItemWithAnotherSpacesPriority_IsRejected()
    {
        Assert.ThrowsAsync<LookupNotInSpaceException>(() =>
            _workItems.CreateAsync("Fence", null, null, _home.ToDo, priorityId: _software.High, spaceId: _home.Id));
    }

    [Test]
    public async Task CreatingAChildWithItsParentsSpaceStatus_IsAllowed_ButNotAnotherSpaces()
    {
        var parent = await _workItems.CreateAsync("House", null, null, _home.ToDo, spaceId: _home.Id);

        Assert.DoesNotThrowAsync(() => _workItems.CreateAsync("Roof", null, parent.Id, _home.Doing));
        Assert.ThrowsAsync<LookupNotInSpaceException>(() =>
            _workItems.CreateAsync("Garage", null, parent.Id, _software.Doing));
    }

    [Test]
    public async Task ChangingStatusToAnotherSpacesStatus_IsRejectedAndChangesNothing()
    {
        var item = await _workItems.CreateAsync("Fence", null, null, _home.ToDo, spaceId: _home.Id);

        Assert.ThrowsAsync<LookupNotInSpaceException>(() =>
            _workItems.ChangeStatusAsync(item.Id, _software.Done));
        Assert.That((await _workItems.GetByIdAsync(item.Id))!.StatusId, Is.EqualTo(_home.ToDo));
    }

    [Test]
    public async Task SettingAnotherSpacesPriority_IsRejected_ButItsOwnIsSaved()
    {
        var item = await _workItems.CreateAsync("Fence", null, null, _home.ToDo, spaceId: _home.Id);

        Assert.ThrowsAsync<LookupNotInSpaceException>(() =>
            _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { PriorityId = (Guid?)_software.Urgent }));
        await _workItems.UpdateDetailsAsync(item.Id, new WorkItemDetailsPatch { PriorityId = (Guid?)_home.Urgent });

        Assert.That((await _workItems.GetByIdAsync(item.Id))!.PriorityId, Is.EqualTo(_home.Urgent));
    }
}
