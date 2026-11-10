using Microsoft.EntityFrameworkCore;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

[TestFixture]
public class WorkItemUrlLinkServiceTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private WorkItemUrlLinkService _urlLinks = null!;
    private WorkItemService _workItems = null!;
    private TestSpace _space = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        _urlLinks = new WorkItemUrlLinkService(_db, FakeCallerContext.Anyone());
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), new FixedServerCalendar(), FakeCallerContext.Anyone());
        _space = TestSpaces.Add(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task CreateAsync_AddsAUrlLinkToTheItem()
    {
        var item = await CreateAsync("Kitchen");

        var link = await _urlLinks.CreateAsync(item.Id, " https://example.com/quote ", " Quote ");

        Assert.Multiple(() =>
        {
            Assert.That(link.Url, Is.EqualTo("https://example.com/quote"));
            Assert.That(link.Label, Is.EqualTo("Quote"));
        });
    }

    [Test]
    public void CreateAsync_ForAnUnknownItem_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _urlLinks.CreateAsync(Guid.NewGuid(), "https://example.com", null));
    }

    [Test]
    public async Task CreateAsync_WithAnInvalidUrl_ThrowsDomainValidationException()
    {
        var item = await CreateAsync("Kitchen");

        Assert.ThrowsAsync<DomainValidationException>(() => _urlLinks.CreateAsync(item.Id, "javascript:alert(1)", null));
    }

    [Test]
    public async Task ListForWorkItemAsync_ReturnsOnlyThatItemsLinks_InCreationOrder()
    {
        var item = await CreateAsync("Kitchen");
        var other = await CreateAsync("Garden");
        await _urlLinks.CreateAsync(item.Id, "https://example.com/1", null);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _urlLinks.CreateAsync(other.Id, "https://example.com/other", null);
        await _urlLinks.CreateAsync(item.Id, "https://example.com/2", "Second");

        var links = await _urlLinks.ListForWorkItemAsync(item.Id);

        Assert.That(links.Select(l => (l.Url, l.Label)), Is.EqualTo(new[]
        {
            ("https://example.com/1", (string?)null),
            ("https://example.com/2", "Second"),
        }));
    }

    [Test]
    public async Task DeleteAsync_RemovesTheUrlLink()
    {
        var item = await CreateAsync("Kitchen");
        var link = await _urlLinks.CreateAsync(item.Id, "https://example.com", null);

        await _urlLinks.DeleteAsync(link.Id);

        Assert.That(await _urlLinks.ListForWorkItemAsync(item.Id), Is.Empty);
    }

    [Test]
    public void DeleteAsync_WhenNotFound_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() => _urlLinks.DeleteAsync(Guid.NewGuid()));
    }

    [Test]
    public async Task ATrashedItemsUrlLinks_AreKeptButHidden_UntilItIsRestored()
    {
        var trash = new WorkItemTrashService(_db, new IterativeWorkItemHierarchy(_db), FakeCallerContext.Anyone(), _clock);
        var item = await CreateAsync("Kitchen");
        var link = await _urlLinks.CreateAsync(item.Id, "https://example.com", null);

        await trash.MoveToTrashAsync(item.Id);

        Assert.Multiple(async () =>
        {
            Assert.That(await _urlLinks.ListForWorkItemAsync(item.Id), Is.Empty);
            Assert.That(await _db.WorkItemUrlLinks.IgnoreQueryFilters().CountAsync(), Is.EqualTo(1));
            Assert.ThrowsAsync<EntityNotFoundException>(() => _urlLinks.DeleteAsync(link.Id));
        });

        await trash.RestoreAsync(item.Id);

        Assert.That(await _urlLinks.ListForWorkItemAsync(item.Id), Has.Count.EqualTo(1));
    }

    private Task<WorkItem> CreateAsync(string title) =>
        _workItems.CreateAsync(title, null, null, _space.ToDo, spaceId: _space.Id);
}
