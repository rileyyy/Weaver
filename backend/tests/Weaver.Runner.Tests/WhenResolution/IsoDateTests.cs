using System.Globalization;
using Weaver.Runner.WhenResolution;

namespace Weaver.Runner.Tests.WhenResolution;

[TestFixture]
public class IsoDateTests
{
    [Test]
    [SetCulture("th-TH")]
    public void Format_IsGregorian_EvenWhenTheCultureUsesAnotherCalendar() =>
        Assert.That(IsoDate.Format(new DateOnly(2026, 11, 11)), Is.EqualTo("2026-11-11"));

    [Test]
    [SetCulture("th-TH")]
    public void TryParse_ReadsGregorianDates_EvenWhenTheCultureUsesAnotherCalendar()
    {
        Assert.Multiple(() =>
        {
            Assert.That(IsoDate.TryParse("2026-11-11", out var date), Is.True);
            Assert.That(date, Is.EqualTo(new DateOnly(2026, 11, 11)));
        });
    }

    [TestCase("12/01/2026")]
    [TestCase("2026-02-30")]
    [TestCase("")]
    [TestCase(null)]
    public void TryParse_RefusesAnythingElse(string? text) =>
        Assert.That(IsoDate.TryParse(text, out _), Is.False);

    [Test]
    public void TheCultureCheckIsMeaningful() =>
        Assert.That(new DateOnly(2026, 11, 11).ToString("yyyy", new CultureInfo("th-TH")), Is.Not.EqualTo("2026"));
}
