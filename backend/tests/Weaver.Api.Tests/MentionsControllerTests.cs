using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;

namespace Weaver.Api.Tests;

[TestFixture]
public class MentionsControllerTests
{
    public static PendingMention MakePending()
    {
        var comment = new CommentView(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "riley", "@claude-research find quotes",
            new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero), null);
        return new PendingMention(comment, new WorkItemReference(42, "Kitchen quotes"), comment.CreatedAtUtc);
    }

    [Test]
    public async Task ListPending_ReturnsTheCallersPendingMentionsWithCommentAuthorAndItem()
    {
        var pending = MakePending();
        var mentions = new Mock<IMentionService>(MockBehavior.Strict);
        mentions.Setup(m => m.ListPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync([pending]);
        var controller = new MentionsController(mentions.Object);

        var result = await controller.ListPending();

        var body = ((result.Result as OkObjectResult)!.Value as IEnumerable<PendingMentionDto>)!.Single();
        Assert.Multiple(() =>
        {
            Assert.That(body.Comment.Id, Is.EqualTo(pending.Comment.Id));
            Assert.That(body.Comment.AuthorUsername, Is.EqualTo("riley"));
            Assert.That(body.Comment.Body, Is.EqualTo("@claude-research find quotes"));
            Assert.That(body.WorkItemId, Is.EqualTo(pending.Comment.WorkItemId));
            Assert.That(body.WorkItemNumber, Is.EqualTo(42));
            Assert.That(body.WorkItemTitle, Is.EqualTo("Kitchen quotes"));
            Assert.That(body.MentionedAtUtc, Is.EqualTo(pending.MentionedAtUtc));
        });
    }
}
