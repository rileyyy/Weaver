using Weaver.Runner.Mentions;

namespace Weaver.Runner.Tests.Mentions;

[TestFixture]
public class MentionAnswerCheckerTests
{
    private static readonly DateOnly Today = new(2026, 11, 11);

    [Test]
    public void AFullAnswer_IsAccepted()
    {
        var check = MentionAnswerChecker.Check(
            """
            {"reply":" Here you go. ","subItems":["Call fitters"],
             "links":[{"url":"https://a.example/x","label":"Quote"},{"url":"http://b.example/"}],
             "start":"2026-11-16","end":null}
            """,
            Today);

        Assert.That(check.Answer, Is.Not.Null, check.RejectReason);
        Assert.Multiple(() =>
        {
            Assert.That(check.Answer!.Reply, Is.EqualTo("Here you go."));
            Assert.That(check.Answer.SubItems, Is.EqualTo(new[] { "Call fitters" }));
            Assert.That(check.Answer.Links, Is.EqualTo(new[]
            {
                new MentionLink("https://a.example/x", "Quote"),
                new MentionLink("http://b.example/", null),
            }));
            Assert.That(check.Answer.Start, Is.EqualTo(new DateOnly(2026, 11, 16)));
            Assert.That(check.Answer.End, Is.Null);
        });
    }

    [Test]
    public void AReplyAlone_IsAccepted_EvenInACodeFence()
    {
        var check = MentionAnswerChecker.Check("```json\n{\"reply\":\"No can do.\"}\n```", Today);

        Assert.Multiple(() =>
        {
            Assert.That(check.Answer!.Reply, Is.EqualTo("No can do."));
            Assert.That(check.Answer.SubItems, Is.Empty);
            Assert.That(check.Answer.Links, Is.Empty);
            Assert.That(check.Answer.HasDates, Is.False);
        });
    }

    [TestCase("Sure, I'll do that.", "isn't a JSON object")]
    [TestCase("""{"subItems":["Call"]}""", "no reply")]
    [TestCase("""{"reply":"   "}""", "no reply")]
    [TestCase("""{"reply":"ok","subItems":"Call"}""", "isn't a list")]
    [TestCase("""{"reply":"ok","subItems":["Call",""]}""", "sub-item")]
    [TestCase("""{"reply":"ok","subItems":[3]}""", "sub-item")]
    [TestCase("""{"reply":"ok","links":[{"label":"x"}]}""", "no url")]
    [TestCase("""{"reply":"ok","links":[{"url":"javascript:alert(1)"}]}""", "http or https")]
    [TestCase("""{"reply":"ok","links":[{"url":"/relative"}]}""", "http or https")]
    [TestCase("""{"reply":"ok","start":"2026-11-10"}""", "before today")]
    [TestCase("""{"reply":"ok","start":"2026-11-20","end":"2026-11-12"}""", "before start")]
    [TestCase("""{"reply":"ok","start":"next week"}""", "isn't a yyyy-MM-dd date")]
    public void AnAnswerThatFailsACheck_IsRejectedWithTheReason(string raw, string reason)
    {
        var check = MentionAnswerChecker.Check(raw, Today);

        Assert.Multiple(() =>
        {
            Assert.That(check.Answer, Is.Null);
            Assert.That(check.RejectReason, Does.Contain(reason));
        });
    }

    [Test]
    public void TooManyAdditions_AreRejected()
    {
        var titles = string.Join(",", Enumerable.Range(1, MentionAnswerChecker.MaxSubItems + 1).Select(i => $"\"Step {i}\""));

        var check = MentionAnswerChecker.Check($$"""{"reply":"ok","subItems":[{{titles}}]}""", Today);

        Assert.That(check.RejectReason, Does.Contain($"more than {MentionAnswerChecker.MaxSubItems}"));
    }
}
