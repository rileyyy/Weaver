using Weaver.Application.Services;

namespace Weaver.Application.Tests;

[TestFixture]
public class WorkItemNumberTextTests
{
    [TestCase("#123", 123)]
    [TestCase("  #7 ", 7)]
    public void AHashAndDigits_IsThatNumber(string text, int expected)
    {
        Assert.Multiple(() =>
        {
            Assert.That(WorkItemNumberText.TryParse(text, out var number), Is.True);
            Assert.That(number, Is.EqualTo(expected));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("#")]
    [TestCase("123")]
    [TestCase("#abc")]
    [TestCase("#-1")]
    [TestCase("#1 2")]
    [TestCase("#99999999999")]
    [TestCase("fix #12")]
    public void AnythingElse_IsNotANumber(string? text)
    {
        Assert.That(WorkItemNumberText.TryParse(text, out _), Is.False);
    }
}
