using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure.Configurations;
using Weaver.Infrastructure.Services;

namespace Weaver.Infrastructure.Tests;

[TestFixture]
public class CommentServiceTests
{
    private WeaverDbContext _db = null!;
    private CommentService _comments = null!;
    private WorkItemService _workItems = null!;
    private Guid _authorId;
    private Guid _otherUserId;

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<WeaverDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WeaverDbContext(options);
        _db.Database.EnsureCreated();
        _comments = new CommentService(_db);
        _workItems = new WorkItemService(_db);

        _authorId = Guid.NewGuid();
        _otherUserId = Guid.NewGuid();
        _db.Users.AddRange(
            MakeUser(_authorId, "alice"),
            MakeUser(_otherUserId, "bob"));
        await _db.SaveChangesAsync();
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private static User MakeUser(Guid id, string username) => new()
    {
        Id = id,
        Username = username,
        NormalizedUsername = username,
        PasswordHash = "hash",
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

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

        var updated = await _comments.UpdateAsync(comment.Id, _authorId, "Edited");

        Assert.That(updated.Body, Is.EqualTo("Edited"));
        Assert.That(updated.UpdatedAtUtc, Is.Not.Null);
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
