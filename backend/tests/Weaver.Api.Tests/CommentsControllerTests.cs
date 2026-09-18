using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Domain;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Tests;

[TestFixture]
public class CommentsControllerTests
{
    private Mock<ICommentService> _comments = null!;
    private CommentsController _controller = null!;
    private Guid _userId;

    [SetUp]
    public void SetUp()
    {
        _comments = new Mock<ICommentService>(MockBehavior.Strict);
        _controller = new CommentsController(_comments.Object);
        _userId = Guid.NewGuid();

        var identity = new ClaimsIdentity([new Claim("sub", _userId.ToString())], authenticationType: "Test");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
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
    public async Task ListForWorkItem_DelegatesToServiceAndReturnsOk()
    {
        var workItemId = Guid.NewGuid();
        var comment = MakeComment(workItemId, _userId);
        _comments.Setup(c => c.ListForWorkItemAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([comment]);

        var result = await _controller.ListForWorkItem(workItemId);

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var body = (ok!.Value as IEnumerable<CommentDto>)!.ToList();
        Assert.That(body.Single().Id, Is.EqualTo(comment.Id));
    }

    [Test]
    public async Task Create_UsesTheAuthenticatedUserAsAuthor()
    {
        var workItemId = Guid.NewGuid();
        var comment = MakeComment(workItemId, _userId);
        _comments.Setup(c => c.CreateAsync(workItemId, _userId, "Hello", It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        var result = await _controller.Create(workItemId, new CreateCommentRequest("Hello"));

        Assert.That((result.Result as OkObjectResult)!.Value, Is.EqualTo(CommentDto.FromEntity(comment)));
        _comments.VerifyAll();
    }

    [Test]
    public async Task Update_PassesTheAuthenticatedUserAsTheRequestingUser()
    {
        var workItemId = Guid.NewGuid();
        var comment = MakeComment(workItemId, _userId);
        _comments.Setup(c => c.UpdateAsync(comment.Id, _userId, "Edited", It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        var result = await _controller.Update(comment.Id, new UpdateCommentRequest("Edited"));

        Assert.That((result.Result as OkObjectResult)!.Value, Is.EqualTo(CommentDto.FromEntity(comment)));
        _comments.VerifyAll();
    }

    [Test]
    public async Task Delete_PassesTheAuthenticatedUserAsTheRequestingUser_AndReturnsNoContent()
    {
        var commentId = Guid.NewGuid();
        _comments.Setup(c => c.DeleteAsync(commentId, _userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.Delete(commentId);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        _comments.VerifyAll();
    }
}
