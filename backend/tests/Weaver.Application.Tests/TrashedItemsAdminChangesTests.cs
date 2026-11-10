using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>
/// Admin changes that re-point every item using a value reach trashed items too, so a restore
/// never brings back an item pointing at something that's gone (ADR-0007).
/// </summary>
[TestFixture]
public class TrashedItemsAdminChangesTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private FakeCallerContext _caller = null!;
    private WorkItemService _workItems = null!;
    private WorkItemTrashService _trash = null!;
    private User _admin = null!;
    private User _alice = null!;
    private TestSpace _home = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 4, 9, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        _admin = TestUsers.Add(_db, "admin", isAdmin: true);
        _alice = TestUsers.Add(_db, "alice");
        _home = TestSpaces.Add(_db, "Home");
        _caller = new FakeCallerContext(_admin.Id);
        var hierarchy = new IterativeWorkItemHierarchy(_db);
        _workItems = new WorkItemService(_db, hierarchy, new FixedServerCalendar(), _caller);
        _trash = new WorkItemTrashService(_db, hierarchy, _caller, _clock);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private async Task<WorkItem> CreateTrashedAsync(Guid? statusId = null, Guid? typeId = null, Guid? priorityId = null)
    {
        var item = await _workItems.CreateAsync(
            "Old shed", null, null, statusId ?? _home.ToDo, typeId: typeId, priorityId: priorityId, spaceId: _home.Id);
        await _trash.MoveToTrashAsync(item.Id);
        return item;
    }

    private async Task<WorkItem> RestoredAsync(Guid id)
    {
        _db.ChangeTracker.Clear();
        return await _trash.RestoreAsync(id);
    }

    [Test]
    public async Task DeletingAStatus_UsedOnlyInTheTrash_StillNeedsAReplacement()
    {
        await CreateTrashedAsync(statusId: _home.Doing);
        var statuses = new StatusService(_db, new AdminAuthorizer(_db), new FixedServerCalendar(), _caller);

        Assert.ThrowsAsync<LookupValueInUseException>(() => statuses.DeleteAsync(_admin.Id, _home.Doing, null));
    }

    [Test]
    public async Task DeletingAStatus_MovesTrashedItemsToTheReplacement()
    {
        var item = await CreateTrashedAsync(statusId: _home.Doing);

        await new StatusService(_db, new AdminAuthorizer(_db), new FixedServerCalendar(), _caller).DeleteAsync(_admin.Id, _home.Doing, _home.Done);

        Assert.That((await RestoredAsync(item.Id)).StatusId, Is.EqualTo(_home.Done));
    }

    [Test]
    public async Task DeletingAType_MovesTrashedItemsToTheReplacement()
    {
        var item = await CreateTrashedAsync(typeId: _home.Task);

        await new WorkItemTypeService(_db, new AdminAuthorizer(_db), _caller).DeleteAsync(_admin.Id, _home.Task, _home.Goal);

        Assert.That((await RestoredAsync(item.Id)).TypeId, Is.EqualTo(_home.Goal));
    }

    [Test]
    public async Task DeletingAPriority_MovesTrashedItemsToTheReplacement()
    {
        var item = await CreateTrashedAsync(priorityId: _home.Low);

        await new WorkItemPriorityService(_db, new AdminAuthorizer(_db), _caller).DeleteAsync(_admin.Id, _home.Low, _home.High);

        Assert.That((await RestoredAsync(item.Id)).PriorityId, Is.EqualTo(_home.High));
    }

    [Test]
    public async Task DeletingACustomField_ClearsItsValueOnTrashedItems()
    {
        var field = CustomFieldDefinition.Create(_home.Project, "Budget", CustomFieldKind.Currency, 0, []);
        _db.CustomFields.Add(field);
        await _db.SaveChangesAsync();
        var item = await _workItems.CreateAsync("Old shed", null, null, _home.ToDo, typeId: _home.Project, spaceId: _home.Id);
        await _workItems.SetCustomFieldsAsync(
            item.Id, new Dictionary<Guid, JsonElement> { [field.Id] = JsonSerializer.Deserialize<JsonElement>("1200") });
        await _trash.MoveToTrashAsync(item.Id);

        await new CustomFieldService(_db, new AdminAuthorizer(_db), _caller).DeleteAsync(_admin.Id, field.Id);

        Assert.That((await RestoredAsync(item.Id)).CustomFields, Is.Empty);
    }

    [Test]
    public async Task DeactivatingAUser_UnassignsTheirTrashedItemsToo()
    {
        var item = await _workItems.CreateAsync("Old shed", null, null, _home.ToDo, spaceId: _home.Id);
        await _workItems.AssignAsync(item.Id, _alice.Id);
        await _trash.MoveToTrashAsync(item.Id);

        await UserAdministration().SetActiveAsync(_admin.Id, _alice.Id, isActive: false);

        Assert.That((await RestoredAsync(item.Id)).AssignedToUserId, Is.Null);
    }

    [Test]
    public async Task DeletingAUser_WhoCommentedOnlyOnTrashedItems_IsRefusedForTheirComments()
    {
        var item = await _workItems.CreateAsync("Old shed", null, null, _home.ToDo, spaceId: _home.Id);
        _db.Comments.Add(Comment.Create(item.Id, _alice.Id, "Note"));
        await _db.SaveChangesAsync();
        await _trash.MoveToTrashAsync(item.Id);

        Assert.ThrowsAsync<UserHasCommentsException>(() => UserAdministration().DeleteAsync(_admin.Id, _alice.Id));
    }

    private UserAdministrationService UserAdministration() =>
        new(_db, new AdminAuthorizer(_db), new PasswordHasher<User>(), _clock, _caller);
}
