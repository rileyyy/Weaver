using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class LookupValueTests
{
    [Test]
    public void Create_TrimsTheName()
    {
        var priority = WorkItemPriority.Create("  High ", 2);

        Assert.That(priority.Name, Is.EqualTo("High"));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Create_WithABlankName_IsAValidationError(string name)
    {
        Assert.Throws<DomainValidationException>(() => WorkItemLayer.Create(name, 0));
    }

    [Test]
    public void Rename_WithANameOverTheLimit_IsAValidationErrorAndKeepsTheOldName()
    {
        var layer = WorkItemLayer.Create("Task", 0);

        Assert.Throws<DomainValidationException>(() => layer.Rename(new string('x', LookupValue.NameMaxLength + 1)));
        Assert.That(layer.Name, Is.EqualTo("Task"));
    }

    [Test]
    public void Status_Create_NormalisesTheColorToUpperCase()
    {
        var status = Status.Create("Review", 3, StatusCategory.Doing, " #00897b ");

        Assert.That(status.Color, Is.EqualTo("#00897B"));
    }

    [TestCase("00897B")]
    [TestCase("#0089")]
    [TestCase("#GGGGGG")]
    [TestCase("")]
    public void Status_WithAColorThatIsNotRRGGBB_IsAValidationError(string color)
    {
        Assert.Throws<DomainValidationException>(() => Status.Create("Review", 3, StatusCategory.Doing, color));
    }

    [Test]
    public void Status_WithAnUndefinedCategory_IsAValidationError()
    {
        Assert.Throws<DomainValidationException>(() => Status.Create("Review", 3, (StatusCategory)42, "#00897B"));
    }

    [Test]
    public void Status_Recategorize_ChangesCategoryAndColor()
    {
        var status = Status.Create("Review", 3, StatusCategory.Doing, "#00897B");

        status.Recategorize(StatusCategory.Done, "#123456");

        Assert.Multiple(() =>
        {
            Assert.That(status.Category, Is.EqualTo(StatusCategory.Done));
            Assert.That(status.Color, Is.EqualTo("#123456"));
        });
    }
}
