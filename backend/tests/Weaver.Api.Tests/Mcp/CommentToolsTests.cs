using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using Moq;
using Weaver.Api.Mcp;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class CommentToolsTests
{
    private Mock<ICommentService> _comments = null!;
    private CommentTools _tools = null!;
    private Guid _userId;

    [SetUp]
    public void SetUp()
    {
        _comments = new Mock<ICommentService>(MockBehavior.Strict);
        _userId = Guid.NewGuid();

        var identity = new ClaimsIdentity([new Claim("sub", _userId.ToString())], authenticationType: "Test");
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };

        _tools = new CommentTools(_comments.Object, httpContextAccessor);
    }

    private static Comment MakeComment(Guid workItemId, Guid authorUserId) => new()
    {
        Id = Guid.NewGuid(),
        WorkItemId = workItemId,
        AuthorUserId = authorUserId,
        Body = "A comment",
        CreatedAtUtc = DateTimeOffset.UtcNow,
    };

    [Test]
    public async Task ListComments_DelegatesToServiceAndMapsResults()
    {
        var workItemId = Guid.NewGuid();
        var comment = MakeComment(workItemId, _userId);
        _comments.Setup(c => c.ListForWorkItemAsync(workItemId, It.IsAny<CancellationToken>())).ReturnsAsync([comment]);

        var result = await _tools.ListComments(workItemId, CancellationToken.None);

        Assert.That(result.Single().Id, Is.EqualTo(comment.Id));
    }

    [Test]
    public async Task AddComment_UsesTheAuthenticatedUserAsAuthor()
    {
        var workItemId = Guid.NewGuid();
        var comment = MakeComment(workItemId, _userId);
        _comments.Setup(c => c.CreateAsync(workItemId, _userId, "Hello", It.IsAny<CancellationToken>())).ReturnsAsync(comment);

        var result = await _tools.AddComment(workItemId, "Hello", CancellationToken.None);

        Assert.That(result.Id, Is.EqualTo(comment.Id));
        _comments.VerifyAll();
    }

    [Test]
    public void UpdateComment_WhenNotAuthor_ThrowsMcpExceptionWithSameMessage()
    {
        var commentId = Guid.NewGuid();
        var domainException = new CommentAuthorMismatchException(commentId);
        _comments.Setup(c => c.UpdateAsync(commentId, _userId, "Edited", It.IsAny<CancellationToken>())).ThrowsAsync(domainException);

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            _tools.UpdateComment(commentId, "Edited", CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    [Test]
    public async Task DeleteComment_PassesTheAuthenticatedUserAsTheRequestingUser()
    {
        var commentId = Guid.NewGuid();
        _comments.Setup(c => c.DeleteAsync(commentId, _userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _tools.DeleteComment(commentId, ct: CancellationToken.None);

        _comments.VerifyAll();
    }
}
