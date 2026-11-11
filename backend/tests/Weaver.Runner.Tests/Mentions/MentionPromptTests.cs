using Weaver.Runner.Mentions;
using Weaver.Runner.WeaverApi;

namespace Weaver.Runner.Tests.Mentions;

[TestFixture]
public class MentionPromptTests
{
    private static readonly DateOnly Today = new(2026, 11, 11);
    private static readonly DateTimeOffset Noon = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private static WeaverItemDetail Item(DateOnly? start = null, DateOnly? end = null) =>
        new(Guid.NewGuid(), 7, "Kitchen quotes", null, Guid.NewGuid(), "Home", "To Do", "before Christmas", start, end, 3);

    private static WeaverComment Comment(string author, string body, int minute) =>
        new(Guid.NewGuid(), Guid.NewGuid(), author, body, Noon.AddMinutes(minute));

    private static WeaverPendingMention MentionOf(WeaverComment comment) => new(comment, Guid.NewGuid(), 7, "Kitchen quotes");

    [Test]
    public void TheItemsDatesAreShown_SoTheModelKnowsWhetherItMayAddThem()
    {
        var request = Comment("riley", "@claude plan it", 0);

        var undated = MentionPrompt.Build(MentionOf(request), Item(), [request], Today).User;
        var dated = MentionPrompt.Build(MentionOf(request), Item(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 3)), [request], Today).User;

        Assert.Multiple(() =>
        {
            Assert.That(undated, Does.Contain("Dates: none").And.Contain("When: before Christmas").And.Contain("Today: 2026-11-11 (Wednesday)"));
            Assert.That(dated, Does.Contain("Dates: 2026-12-01 to 2026-12-03"));
        });
    }

    [Test]
    public void CommentsAfterTheRequest_AreLeftOut()
    {
        var request = Comment("riley", "@claude plan it", 1);
        var thread = new[] { Comment("sam", "before", 0), request, Comment("sam", "after", 2) };

        var user = MentionPrompt.Build(MentionOf(request), Item(), thread, Today).User;

        Assert.That(user, Does.Contain("sam: before").And.Not.Contain("sam: after"));
    }

    [Test]
    public void ALongThread_KeepsOnlyTheNewestComments()
    {
        var thread = Enumerable.Range(0, MentionPrompt.MaxThreadComments + 5)
            .Select(i => Comment("riley", $"note {i:D2}", i))
            .ToList();
        var request = thread[^1];

        var user = MentionPrompt.Build(MentionOf(request), Item(), thread, Today).User;

        Assert.That(user, Does.Not.Contain("note 04").And.Contain("note 05"));
    }
}
