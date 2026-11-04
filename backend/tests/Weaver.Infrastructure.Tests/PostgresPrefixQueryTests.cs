namespace Weaver.Infrastructure.Tests;

[TestFixture]
public class PostgresPrefixQueryTests
{
    [Test]
    public void EveryWord_IsRequired_AsAPrefix()
    {
        Assert.That(PostgresPrefixQuery.From("Paint hall"), Is.EqualTo("paint:* & hall:*"));
    }

    [Test]
    public void TsQueryOperatorsAndQuotes_AreDropped_SoTypedTextCantBreakTheQuery()
    {
        Assert.That(PostgresPrefixQuery.From("it's (a) | !test: *&"), Is.EqualTo("it:* & s:* & a:* & test:*"));
    }

    [Test]
    public void LettersOutsideAscii_AreKept()
    {
        Assert.That(PostgresPrefixQuery.From("Größe Café 12"), Is.EqualTo("größe:* & café:* & 12:*"));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("?!-")]
    public void TextWithNoWords_IsNoQuery(string text)
    {
        Assert.That(PostgresPrefixQuery.From(text), Is.Null);
    }

    [Test]
    public void ARepeatedWord_IsAskedForOnce()
    {
        Assert.That(PostgresPrefixQuery.From("hall Hall"), Is.EqualTo("hall:*"));
    }
}
