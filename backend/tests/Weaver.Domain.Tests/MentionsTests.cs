namespace Weaver.Domain.Tests;

[TestFixture]
public class MentionsTests
{
    [Test]
    public void Parse_FindsEachHandleOnce()
    {
        var handles = Mentions.Parse("@claude-research find three quotes, then ask @llama3. Thanks @claude-research!");

        Assert.That(handles, Is.EquivalentTo(new[] { "claude-research", "llama3" }));
    }

    [Test]
    public void Parse_ReadsAMentionAtTheStartOfALineOrAfterPunctuation()
    {
        var handles = Mentions.Parse("Question:\n@first (@second) \"@third\"");

        Assert.That(handles, Is.EquivalentTo(new[] { "first", "second", "third" }));
    }

    [Test]
    public void Parse_LowerCasesAMentionTypedInCapitals()
    {
        Assert.That(Mentions.Parse("@Claude-Research please"), Is.EquivalentTo(new[] { "claude-research" }));
    }

    [Test]
    public void Parse_StopsAtATrailingHyphen()
    {
        Assert.That(Mentions.Parse("@claude- what do you think"), Is.EquivalentTo(new[] { "claude" }));
    }

    [TestCase("mail riley@example.com about it")]
    [TestCase("no mentions here")]
    [TestCase("a lone @ sign")]
    [TestCase("@@double")]
    public void Parse_IgnoresAnAtSignThatIsntAMention(string body)
    {
        Assert.That(Mentions.Parse(body), Is.Empty);
    }
}
