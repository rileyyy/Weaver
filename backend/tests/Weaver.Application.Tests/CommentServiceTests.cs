using Microsoft.EntityFrameworkCore;
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
    private CommentService _comments = null!;
    private WorkItemService _workItems = null!;
    private Guid _authorId;
    private Guid _otherUserId;

    [SetUp]
    public async Task SetUp()
    {
        _clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        _db = TestDatabase.Create(_clock);
        _comments = new CommentService(_db, _clock);
        _workItems = new WorkItemService(_db, new IterativeWorkItemHierarchy(_db));

        var alice = User.CreateHuman("alice", "alice");
        var bob = User.CreateHuman("bob", "bob");
        _authorId = alice.Id;
        _otherUserId = bob.Id;
        _db.Users.AddRange(alice, bob);
        await _db.SaveChangesAsync();
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task CreateAsync_AddsACommentAuthoredByTheGivenUser()
    {
        var item = await _workItems.CreateAsync("Task", null, null, StatusConfiguration.ToDoId);

        var comment = await _comments.CreateAsync(item.Id, _authorId, "Looks good");

        Assert.That(comment.Body, Is.EqualTo("Looks good"));
        Assert.That(comment.AuthorUserId, Is.EqualTo(_authorId));
        Assert.That(comment.UpdatedAtUtc, Is.Null);
    }

    [Test]
    public void CreateAsync_WhenWorkItemNotFound_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _comments.CreateAsync(Guid.NewGuid(), _authorId, "Looks good"));
    }

    [Test]
    public async Task ListForWorkItemAsync_ReturnsCommentsOldestFirst()
    {
        var item = await _workItems.CreateAsync("Task", null, null, StatusConfiguration.ToDoId);
        var first = await _comments.CreateAsync(item.Id, _authorId, "First");
        var second = await _comments.CreateAsync(item.Id, _otherUserId, "Second");

        var comments = await _comments.ListForWorkItemAsync(item.Id);

        Assert.That(comments.Select(c => c.Id), Is.EqualTo(new[] { first.Id, second.Id }));
    }

    [Test]
    public async Task UpdateAsync_ByTheAuthor_ChangesTheBodyAndSetsUpdatedAtUtc()
    {
        var item = await _workItems.CreateAsync("Task", null, null, StatusConfiguration.ToDoId);
        var comment = await _comments.CreateAsync(item.Id, _authorId, "Original");
        _clock.Advance(TimeSpan.FromMinutes(5));

        var updated = await _comments.UpdateAsync(comment.Id, _authorId, "Edited");

        Assert.That(updated.Body, Is.EqualTo("Edited"));
        Assert.That(updated.UpdatedAtUtc, Is.EqualTo(_clock.GetUtcNow()));
        Assert.That(updated.CreatedAtUtc, Is.EqualTo(_clock.GetUtcNow() - TimeSpan.FromMinutes(5)));
    }

    [Test]
    public async Task UpdateAsync_ByAnotherUser_ThrowsCommentAuthorMismatchException()
    {
        var item = await _workItems.CreateAsync("Task", null, null, StatusConfiguration.ToDoId);
        var comment = await _comments.CreateAsync(item.Id, _authorId, "Original");

        Assert.ThrowsAsync<CommentAuthorMismatchException>(() =>
            _comments.UpdateAsync(comment.Id, _otherUserId, "Hijacked"));
    }

    [Test]
    public async Task DeleteAsync_ByTheAuthor_RemovesTheComment()
    {
        var item = await _workItems.CreateAsync("Task", null, null, StatusConfiguration.ToDoId);
        var comment = await _comments.CreateAsync(item.Id, _authorId, "Original");

        await _comments.DeleteAsync(comment.Id, _authorId);

        Assert.That(await _comments.ListForWorkItemAsync(item.Id), Is.Empty);
    }

    [Test]
    public async Task DeleteAsync_ByAnotherUser_ThrowsCommentAuthorMismatchException()
    {
        var item = await _workItems.CreateAsync("Task", null, null, StatusConfiguration.ToDoId);
        var comment = await _comments.CreateAsync(item.Id, _authorId, "Original");

        Assert.ThrowsAsync<CommentAuthorMismatchException>(() =>
            _comments.DeleteAsync(comment.Id, _otherUserId));
    }

    [Test]
    public void DeleteAsync_WhenNotFound_ThrowsEntityNotFoundException()
    {
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _comments.DeleteAsync(Guid.NewGuid(), _authorId));
    }

    [TestCase("")]
    [TestCase("  ")]
    public async Task CreateAsync_WithBlankBody_ThrowsDomainValidationException(string body)
    {
        var item = await _workItems.CreateAsync("Task", null, null, StatusConfiguration.ToDoId);

        Assert.ThrowsAsync<DomainValidationException>(() => _comments.CreateAsync(item.Id, _authorId, body));
    }

    [Test]
    public async Task CreateAsync_WithBodyOverMaxLength_ThrowsDomainValidationException()
    {
        var item = await _workItems.CreateAsync("Task", null, null, StatusConfiguration.ToDoId);
        var body = new string('a', Comment.BodyMaxLength + 1);

        Assert.ThrowsAsync<DomainValidationException>(() => _comments.CreateAsync(item.Id, _authorId, body));
    }

    [Test]
    public async Task UpdateAsync_WithBlankBody_ThrowsAndKeepsTheOriginalBody()
    {
        var item = await _workItems.CreateAsync("Task", null, null, StatusConfiguration.ToDoId);
        var comment = await _comments.CreateAsync(item.Id, _authorId, "Looks good");

        Assert.ThrowsAsync<DomainValidationException>(() => _comments.UpdateAsync(comment.Id, _authorId, " "));

        _db.ChangeTracker.Clear();
        Assert.That((await _db.Comments.SingleAsync()).Body, Is.EqualTo("Looks good"));
    }
}
