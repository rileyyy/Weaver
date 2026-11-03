using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;
using Weaver.Infrastructure.Configurations;

namespace Weaver.Application.Tests;

[TestFixture]
public class CommentServiceTests
{
    private FixedTimeProvider _clock = null!;
    private WeaverDbContext _db = null!;
    private FakeCallerContext _caller = null!;
    private CommentService _comments = null!;
    private WorkItemService _workItems = null!;
    private Space _space = null!;
    private Guid _authorId;
    private Guid _otherUserId;

    [SetUp]
    public async Task SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        _caller = FakeCallerContext.Anyone();
        _comments = new CommentService(_db, _clock, _caller);
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db), FakeCallerContext.Anyone());
        _space = TestSpaces.Add(_db);

        var alice = User.CreateHuman("alice", "alice");
        var bob = User.CreateHuman("bob", "bob");
        _authorId = alice.Id;
        _otherUserId = bob.Id;
        _db.Users.AddRange(alice, bob);
        await _db.SaveChangesAsync();
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    /// <summary>The service, called by <paramref name="userId"/>.</summary>
    private CommentService As(Guid userId)
    {
        _caller.Current = new Caller(userId, null, null);
        return _comments;
    }

    [Test]
    public async Task CreateAsync_AddsACommentAuthoredByTheCaller()
    {
        var item = await _workItems.CreateAsync("Task", null, null, StatusConfiguration.ToDoId, spaceId: _space.Id);

        var comment = await As(_authorId).CreateAsync(item.Id, "Looks good");

        Assert.That(comment.Body, Is.EqualTo("Looks good"));
        Assert.That(comment.AuthorUserId, Is.EqualTo(_authorId));
        Assert.That(comment.UpdatedAtUtc, Is.Null);
    }

    [Test]
    public void CreateAsync_WhenWorkItemNotFound_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            As(_authorId).CreateAsync(Guid.NewGuid(), "Looks good"));
    }

    [Test]
    public async Task ListForWorkItemAsync_ReturnsCommentsOldestFirst()
    {
        var item = await _workItems.CreateAsync("Task", null, null, StatusConfiguration.ToDoId, spaceId: _space.Id);
        var first = await As(_authorId).CreateAsync(item.Id, "First");
        var second = await As(_otherUserId).CreateAsync(item.Id, "Second");

        var comments = await _comments.ListForWorkItemAsync(item.Id);

        Assert.That(comments.Select(c => c.Id), Is.EqualTo(new[] { first.Id, second.Id }));
    }

    [Test]
    public async Task UpdateAsync_ByTheAuthor_ChangesTheBodyAndSetsUpdatedAtUtc()
    {
        var item = await _workItems.CreateAsync("Task", null, null, StatusConfiguration.ToDoId, spaceId: _space.Id);
        var comment = await As(_authorId).CreateAsync(item.Id, "Original");
        _clock.Advance(TimeSpan.FromMinutes(5));

        var updated = await As(_authorId).UpdateAsync(comment.Id, "Edited");

        Assert.That(updated.Body, Is.EqualTo("Edited"));
        Assert.That(updated.UpdatedAtUtc, Is.EqualTo(_clock.GetUtcNow()));
        Assert.That(updated.CreatedAtUtc, Is.EqualTo(_clock.GetUtcNow() - TimeSpan.FromMinutes(5)));
    }

    [Test]
    public async Task UpdateAsync_ByAnotherUser_ThrowsCommentAuthorMismatchException()
    {
        var item = await _workItems.CreateAsync("Task", null, null, StatusConfiguration.ToDoId, spaceId: _space.Id);
        var comment = await As(_authorId).CreateAsync(item.Id, "Original");

        Assert.ThrowsAsync<CommentAuthorMismatchException>(() =>
            As(_otherUserId).UpdateAsync(comment.Id, "Hijacked"));
    }

    [Test]
    public async Task DeleteAsync_ByTheAuthor_RemovesTheComment()
    {
        var item = await _workItems.CreateAsync("Task", null, null, StatusConfiguration.ToDoId, spaceId: _space.Id);
        var comment = await As(_authorId).CreateAsync(item.Id, "Original");

        await As(_authorId).DeleteAsync(comment.Id);

        Assert.That(await _comments.ListForWorkItemAsync(item.Id), Is.Empty);
    }

    [Test]
    public async Task DeleteAsync_ByAnotherUser_ThrowsCommentAuthorMismatchException()
    {
        var item = await _workItems.CreateAsync("Task", null, null, StatusConfiguration.ToDoId, spaceId: _space.Id);
        var comment = await As(_authorId).CreateAsync(item.Id, "Original");

        Assert.ThrowsAsync<CommentAuthorMismatchException>(() =>
            As(_otherUserId).DeleteAsync(comment.Id));
    }

    [Test]
    public void DeleteAsync_WhenNotFound_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            As(_authorId).DeleteAsync(Guid.NewGuid()));
    }

    [TestCase("")]
    [TestCase("  ")]
    public async Task CreateAsync_WithBlankBody_ThrowsDomainValidationException(string body)
    {
        var item = await _workItems.CreateAsync("Task", null, null, StatusConfiguration.ToDoId, spaceId: _space.Id);

        Assert.ThrowsAsync<DomainValidationException>(() => As(_authorId).CreateAsync(item.Id, body));
    }

    [Test]
    public async Task CreateAsync_WithBodyOverMaxLength_ThrowsDomainValidationException()
    {
        var item = await _workItems.CreateAsync("Task", null, null, StatusConfiguration.ToDoId, spaceId: _space.Id);
        var body = new string('a', Comment.BodyMaxLength + 1);

        Assert.ThrowsAsync<DomainValidationException>(() => As(_authorId).CreateAsync(item.Id, body));
    }

    [Test]
    public async Task UpdateAsync_WithBlankBody_ThrowsAndKeepsTheOriginalBody()
    {
        var item = await _workItems.CreateAsync("Task", null, null, StatusConfiguration.ToDoId, spaceId: _space.Id);
        var comment = await As(_authorId).CreateAsync(item.Id, "Looks good");

        Assert.ThrowsAsync<DomainValidationException>(() => As(_authorId).UpdateAsync(comment.Id, " "));

        _db.ChangeTracker.Clear();
        Assert.That((await _db.Comments.SingleAsync()).Body, Is.EqualTo("Looks good"));
    }
}
