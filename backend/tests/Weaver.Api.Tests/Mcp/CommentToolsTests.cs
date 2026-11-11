using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using Moq;
using Weaver.Api.Auth;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class CommentToolsTests
{
    private Mock<ICommentService> _comments = null!;
    private CommentTools _tools = null!;
    private HttpCallerContext _caller = null!;
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

        _caller = new HttpCallerContext(httpContextAccessor);
        _tools = new CommentTools(_comments.Object, _caller);
    }

    private static CommentView MakeComment(Guid workItemId, Guid authorUserId) =>
        new(Guid.NewGuid(), workItemId, authorUserId, "alice", "A comment", DateTimeOffset.UtcNow, null);

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
    public async Task AddComment_PassesTheActorLabelAndRunIdToTheCallerContext()
    {
        var workItemId = Guid.NewGuid();
        var comment = MakeComment(workItemId, _userId);
        _comments.Setup(c => c.CreateAsync(workItemId, "Hello", It.IsAny<CancellationToken>())).ReturnsAsync(comment);

        var result = await _tools.AddComment(workItemId, "Hello", "sub-agent: reviewer", "run-7", CancellationToken.None);

        Assert.That(result.Id, Is.EqualTo(comment.Id));
        Assert.That(_caller.Current.ActorLabel, Is.EqualTo("sub-agent: reviewer"));
        Assert.That(_caller.Current.RunId, Is.EqualTo("run-7"));
        _comments.VerifyAll();
    }

    [Test]
    public void UpdateComment_WhenNotAuthor_ThrowsMcpExceptionWithSameMessage()
    {
        var commentId = Guid.NewGuid();
        var domainException = new CommentAuthorMismatchException(commentId);
        _comments.Setup(c => c.UpdateAsync(commentId, "Edited", It.IsAny<CancellationToken>())).ThrowsAsync(domainException);

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            _tools.UpdateComment(commentId, "Edited", ct: CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    [Test]
    public async Task DeleteComment_DelegatesToTheService()
    {
        var commentId = Guid.NewGuid();
        _comments.Setup(c => c.DeleteAsync(commentId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _tools.DeleteComment(commentId, ct: CancellationToken.None);

        _comments.VerifyAll();
    }
}
