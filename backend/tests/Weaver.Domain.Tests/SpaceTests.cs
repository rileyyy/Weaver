using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class SpaceTests
{
    [TestCase("")]
    [TestCase("  ")]
    public void Create_WithABlankName_IsRejected(string name)
    {
        Assert.Throws<DomainValidationException>(() => Space.Create(name));
    }

    [Test]
    public void Create_WithATooLongName_IsRejected()
    {
        Assert.Throws<DomainValidationException>(() => Space.Create(new string('x', Space.NameMaxLength + 1)));
    }

    [Test]
    public void Create_TrimsTheName()
    {
        var space = Space.Create("  Village ");

        Assert.That(space.Name, Is.EqualTo("Village"));
    }

    [Test]
    public void Rename_ReportsTheOldAndNewName()
    {
        var space = Space.Create("Home");

        var changes = space.Rename(" House ");

        Assert.Multiple(() =>
        {
            Assert.That(space.Name, Is.EqualTo("House"));
            Assert.That(changes, Is.EqualTo(new[] { new FieldChange(SpaceHistoryField.Name, "Home", "House") }));
        });
    }

    [Test]
    public void Rename_ToTheCurrentName_ReportsNothing()
    {
        var space = Space.Create("Home");

        Assert.That(space.Rename("Home"), Is.Empty);
    }

    [Test]
    public void Rename_ToABlankName_IsRejected()
    {
        var space = Space.Create("Home");

        Assert.Throws<DomainValidationException>(() => space.Rename(" "));
    }

    [Test]
    public void Create_HidesDoneItemsAfterTwoWeeks()
    {
        Assert.That(Space.Create("Home").DoneHidingDays, Is.EqualTo(14));
    }

    [TestCase(1)]
    [TestCase(30)]
    [TestCase(3650)]
    public void SetDoneHidingPeriod_AcceptsOneDayToTenYears_AndReportsTheChange(int days)
    {
        var space = Space.Create("Home");

        var changes = space.SetDoneHidingPeriod(days);

        Assert.Multiple(() =>
        {
            Assert.That(space.DoneHidingDays, Is.EqualTo(days));
            Assert.That(changes, Is.EqualTo(new[] { new FieldChange(SpaceHistoryField.DoneHidingDays, 14, days) }));
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(3651)]
    public void SetDoneHidingPeriod_RejectsAnythingOutsideOneDayToTenYears(int days)
    {
        var space = Space.Create("Home");

        Assert.Throws<DomainValidationException>(() => space.SetDoneHidingPeriod(days));
        Assert.That(space.DoneHidingDays, Is.EqualTo(14));
    }

    [Test]
    public void SetDoneHidingPeriod_ToTheCurrentPeriod_ReportsNothing()
    {
        var space = Space.Create("Home");

        Assert.That(space.SetDoneHidingPeriod(14), Is.Empty);
    }
}
