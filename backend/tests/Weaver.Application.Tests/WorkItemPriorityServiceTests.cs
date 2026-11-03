using Microsoft.EntityFrameworkCore;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>
/// Also covers the behaviour every lookup shares through <see cref="LookupService{T}"/>
/// (admin checks, ordering, name uniqueness, replacement rules), using priorities.
/// </summary>
[TestFixture]
public class WorkItemPriorityServiceTests
{
    private WeaverDbContext _db = null!;
    private WorkItemPriorityService _priorities = null!;
    private User _admin = null!;
    private TestSpace _space = null!;
    private User _member = null!;

    [SetUp]
    public void SetUp()
    {
        _db = TestDatabase.Create(new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)));
        _priorities = new WorkItemPriorityService(_db, new AdminAuthorizer(_db), FakeCallerContext.Anyone());
        _admin = TestUsers.Add(_db, "admin", isAdmin: true);
        _member = TestUsers.Add(_db, "member");
        _space = TestSpaces.Add(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task GetAllAsync_ReturnsSeededPrioritiesFromLeastToMostUrgent()
    {
        var priorities = await _priorities.GetAllAsync(_space.Id);

        Assert.That(priorities.Select(p => p.Name), Is.EqualTo(new[] { "Low", "Medium", "High", "Urgent" }));
    }

    [Test]
    public async Task CreateAsync_AppendsATrimmedValueAfterTheLastOne()
    {
        var created = await _priorities.CreateAsync(_admin.Id, _space.Id, "  Critical ");

        var priorities = await _priorities.GetAllAsync(_space.Id);
        Assert.Multiple(() =>
        {
            Assert.That(created.Name, Is.EqualTo("Critical"));
            Assert.That(priorities.Last().Id, Is.EqualTo(created.Id));
            Assert.That(created.Order, Is.EqualTo(4));
        });
    }

    [Test]
    public async Task CreateAsync_ByANonAdmin_IsForbiddenAndAddsNothing()
    {
        Assert.ThrowsAsync<AdminRequiredException>(() => _priorities.CreateAsync(_member.Id, _space.Id, "Critical"));
        Assert.That(await _db.WorkItemPriorities.CountAsync(), Is.EqualTo(4));
    }

    [Test]
    public void CreateAsync_ByADeactivatedAdmin_IsForbidden()
    {
        var former = TestUsers.Add(_db, "former", isAdmin: true);
        former.Deactivate(DateTimeOffset.UtcNow);
        _db.SaveChanges();

        Assert.ThrowsAsync<AdminRequiredException>(() => _priorities.CreateAsync(former.Id, _space.Id, "Critical"));
    }

    [Test]
    public void CreateAsync_WithABlankName_IsAValidationError()
    {
        Assert.ThrowsAsync<DomainValidationException>(() => _priorities.CreateAsync(_admin.Id, _space.Id, "   "));
    }

    [Test]
    public void CreateAsync_WithAnExistingNameInAnyCase_IsAConflict()
    {
        Assert.ThrowsAsync<LookupNameTakenException>(() => _priorities.CreateAsync(_admin.Id, _space.Id, "high"));
    }

    [Test]
    public async Task RenameAsync_ChangesTheName()
    {
        await _priorities.RenameAsync(_admin.Id, _space.Urgent, "Blocker");

        var urgent = await _db.WorkItemPriorities.AsNoTracking().SingleAsync(p => p.Id == _space.Urgent);
        Assert.That(urgent.Name, Is.EqualTo("Blocker"));
    }

    [Test]
    public async Task RenameAsync_ToItsOwnNameInADifferentCase_IsAllowed()
    {
        var renamed = await _priorities.RenameAsync(_admin.Id, _space.High, "HIGH");

        Assert.That(renamed.Name, Is.EqualTo("HIGH"));
    }

    [Test]
    public void RenameAsync_ToAnotherValuesName_IsAConflict()
    {
        Assert.ThrowsAsync<LookupNameTakenException>(() =>
            _priorities.RenameAsync(_admin.Id, _space.High, "Low"));
    }

    [Test]
    public void RenameAsync_ForAMissingValue_IsNotFound()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _priorities.RenameAsync(_admin.Id, Guid.NewGuid(), "X"));
    }

    [Test]
    public void RenameAsync_ByANonAdmin_IsForbidden()
    {
        Assert.ThrowsAsync<AdminRequiredException>(() =>
            _priorities.RenameAsync(_member.Id, _space.High, "X"));
    }

    [Test]
    public async Task ReorderAsync_StoresTheNewOrder()
    {
        Guid[] newOrder =
        [
            _space.Urgent,
            _space.Low,
            _space.High,
            _space.Medium,
        ];

        var result = await _priorities.ReorderAsync(_admin.Id, _space.Id, newOrder);

        var stored = await _priorities.GetAllAsync(_space.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result.Select(p => p.Id), Is.EqualTo(newOrder));
            Assert.That(stored.Select(p => p.Id), Is.EqualTo(newOrder));
            Assert.That(stored.Select(p => p.Order), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        });
    }

    [Test]
    public void ReorderAsync_ThatLeavesAValueOut_IsAValidationError()
    {
        Guid[] partial = [_space.Urgent, _space.Low];

        Assert.ThrowsAsync<DomainValidationException>(() => _priorities.ReorderAsync(_admin.Id, _space.Id, partial));
    }

    [Test]
    public void ReorderAsync_WithADuplicateOrUnknownId_IsAValidationError()
    {
        Guid[] duplicated =
        [
            _space.Urgent,
            _space.Urgent,
            _space.High,
            _space.Medium,
        ];
        Guid[] unknown =
        [
            Guid.NewGuid(),
            _space.Low,
            _space.High,
            _space.Medium,
        ];

        Assert.Multiple(() =>
        {
            Assert.ThrowsAsync<DomainValidationException>(() => _priorities.ReorderAsync(_admin.Id, _space.Id, duplicated));
            Assert.ThrowsAsync<DomainValidationException>(() => _priorities.ReorderAsync(_admin.Id, _space.Id, unknown));
        });
    }

    [Test]
    public void ReorderAsync_ByANonAdmin_IsForbidden()
    {
        Assert.ThrowsAsync<AdminRequiredException>(() => _priorities.ReorderAsync(_member.Id, _space.Id, []));
    }

    [Test]
    public async Task DeleteAsync_WithAReplacement_MovesItsItemsToIt()
    {
        var item = AddItem(_space.Urgent);

        await _priorities.DeleteAsync(_admin.Id, _space.Urgent, _space.High);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await Reload(item)).PriorityId, Is.EqualTo(_space.High));
            Assert.That(await _db.WorkItemPriorities.AnyAsync(p => p.Id == _space.Urgent), Is.False);
        });
    }

    [Test]
    public async Task DeleteAsync_WithoutAReplacement_LeavesItsItemsWithNoPriority()
    {
        var item = AddItem(_space.Urgent);

        await _priorities.DeleteAsync(_admin.Id, _space.Urgent, replacementId: null);

        Assert.That((await Reload(item)).PriorityId, Is.Null);
    }

    [Test]
    public async Task DeleteAsync_LeavesItemsWithOtherPrioritiesAlone()
    {
        var other = AddItem(_space.Low);

        await _priorities.DeleteAsync(_admin.Id, _space.Urgent, _space.High);

        Assert.That((await Reload(other)).PriorityId, Is.EqualTo(_space.Low));
    }

    [Test]
    public async Task DeleteAsync_TheLastPriority_IsAllowed()
    {
        foreach (var id in new[] { _space.Low, _space.Medium, _space.High })
        {
            await _priorities.DeleteAsync(_admin.Id, id, null);
        }

        await _priorities.DeleteAsync(_admin.Id, _space.Urgent, null);

        Assert.That(await _db.WorkItemPriorities.AnyAsync(), Is.False);
    }

    [Test]
    public void DeleteAsync_ReplacedByItself_IsAValidationError()
    {
        Assert.ThrowsAsync<DomainValidationException>(() =>
            _priorities.DeleteAsync(_admin.Id, _space.High, _space.High));
    }

    [Test]
    public async Task DeleteAsync_WithAMissingReplacement_IsNotFoundAndDeletesNothing()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _priorities.DeleteAsync(_admin.Id, _space.High, Guid.NewGuid()));
        Assert.That(await _db.WorkItemPriorities.CountAsync(), Is.EqualTo(4));
    }

    [Test]
    public void DeleteAsync_ForAMissingValue_IsNotFound()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _priorities.DeleteAsync(_admin.Id, Guid.NewGuid(), null));
    }

    [Test]
    public async Task DeleteAsync_ByANonAdmin_IsForbiddenAndDeletesNothing()
    {
        Assert.ThrowsAsync<AdminRequiredException>(() =>
            _priorities.DeleteAsync(_member.Id, _space.High, null));
        Assert.That(await _db.WorkItemPriorities.CountAsync(), Is.EqualTo(4));
    }

    private WorkItem AddItem(Guid priorityId)
    {
        var item = WorkItem.Create(_space.Id, "Item", null, _space.ToDo, 1, priorityId: priorityId);
        _db.WorkItems.Add(item);
        _db.SaveChanges();
        return item;
    }

    private Task<WorkItem> Reload(WorkItem item) =>
        _db.WorkItems.AsNoTracking().SingleAsync(w => w.Id == item.Id);
}
