using Weaver.Runner.Model;

namespace Weaver.Runner.Tests.Model;

[TestFixture]
public class TextShortenerTests
{
    [TestCase("")]
    [TestCase("abc")]
    [TestCase("abcde")]
    public void TextThatFits_IsUnchanged(string text) =>
        Assert.That(TextShortener.Shorten(text, 5), Is.EqualTo(text));

    [Test]
    public void TextOneOverTheLimit_IsCutToTheLimit_EllipsisIncluded() =>
        Assert.That(TextShortener.Shorten("abcdef", 5), Is.EqualTo("abcd…"));

    [Test]
    public void LongText_IsExactlyTheLimit() =>
        Assert.That(TextShortener.Shorten(new string('a', 100), 10), Has.Length.EqualTo(10).And.EndWith("…"));

    [Test]
    public void ALimitOfOne_LeavesOnlyTheEllipsis() =>
        Assert.That(TextShortener.Shorten("abc", 1), Is.EqualTo("…"));

    [Test]
    public void ALimitTooSmallForTheEllipsis_IsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TextShortener.Shorten("abc", 0));

    [Test]
    public void ACutNeverSplitsASurrogatePair()
    {
        // "ab" then an emoji (two UTF-16 chars); a limit of 4 would otherwise cut between its halves.
        var shortened = TextShortener.Shorten("ab\U0001F600cd", 4);

        Assert.That(shortened, Is.EqualTo("ab…"));
    }
}
