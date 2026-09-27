using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class CommentTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void Create_WithABlankBody_IsRejected()
    {
        Assert.Throws<DomainValidationException>(() => Comment.Create(Guid.NewGuid(), Guid.NewGuid(), " "));
    }

    [Test]
    public void Edit_ByTheAuthor_ChangesTheBodyAndRecordsWhen()
    {
        var author = Guid.NewGuid();
        var comment = Comment.Create(Guid.NewGuid(), author, "Original");

        comment.Edit(author, "Edited", Now);

        Assert.Multiple(() =>
        {
            Assert.That(comment.Body, Is.EqualTo("Edited"));
            Assert.That(comment.UpdatedAtUtc, Is.EqualTo(Now));
        });
    }

    [Test]
    public void Edit_BySomeoneElse_IsRejectedAndLeavesTheBody()
    {
        var comment = Comment.Create(Guid.NewGuid(), Guid.NewGuid(), "Original");

        Assert.Throws<CommentAuthorMismatchException>(() => comment.Edit(Guid.NewGuid(), "Hijacked", Now));
        Assert.That(comment.Body, Is.EqualTo("Original"));
    }

    [Test]
    public void Edit_ToABlankBody_IsRejected()
    {
        var author = Guid.NewGuid();
        var comment = Comment.Create(Guid.NewGuid(), author, "Original");

        Assert.Throws<DomainValidationException>(() => comment.Edit(author, "", Now));
    }
}
