using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class LookupValueTests
{
    [Test]
    public void Create_TrimsTheName()
    {
        var priority = WorkItemPriority.Create(Guid.NewGuid(), "  High ", 2);

        Assert.That(priority.Name, Is.EqualTo("High"));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Create_WithABlankName_IsAValidationError(string name)
    {
        Assert.Throws<DomainValidationException>(() => WorkItemType.Create(Guid.NewGuid(), name, 0, "#43A047"));
    }

    [Test]
    public void Rename_WithANameOverTheLimit_IsAValidationErrorAndKeepsTheOldName()
    {
        var type = WorkItemType.Create(Guid.NewGuid(), "Task", 0, "#43A047");

        Assert.Throws<DomainValidationException>(() => type.Rename(new string('x', LookupValue.NameMaxLength + 1)));
        Assert.That(type.Name, Is.EqualTo("Task"));
    }

    [Test]
    public void Status_Create_NormalisesTheColorToUpperCase()
    {
        var status = Status.Create(Guid.NewGuid(), "Review", 3, StatusCategory.Doing, " #00897b ");

        Assert.That(status.Color, Is.EqualTo("#00897B"));
    }

    [TestCase("00897B")]
    [TestCase("#0089")]
    [TestCase("#GGGGGG")]
    [TestCase("")]
    public void Status_WithAColorThatIsNotRRGGBB_IsAValidationError(string color)
    {
        Assert.Throws<DomainValidationException>(() => Status.Create(Guid.NewGuid(), "Review", 3, StatusCategory.Doing, color));
    }

    [Test]
    public void Status_WithAnUndefinedCategory_IsAValidationError()
    {
        Assert.Throws<DomainValidationException>(() => Status.Create(Guid.NewGuid(), "Review", 3, (StatusCategory)42, "#00897B"));
    }

    [Test]
    public void Status_Recategorize_ChangesCategoryAndColor()
    {
        var status = Status.Create(Guid.NewGuid(), "Review", 3, StatusCategory.Doing, "#00897B");

        status.Recategorize(StatusCategory.Done, "#123456");

        Assert.Multiple(() =>
        {
            Assert.That(status.Category, Is.EqualTo(StatusCategory.Done));
            Assert.That(status.Color, Is.EqualTo("#123456"));
        });
    }

    [Test]
    public void Type_Create_NormalisesTheColorToUpperCase()
    {
        var type = WorkItemType.Create(Guid.NewGuid(), "Epic", 3, " #d81b60 ");

        Assert.That(type.Color, Is.EqualTo("#D81B60"));
    }

    [TestCase("D81B60")]
    [TestCase("#GGGGGG")]
    [TestCase("")]
    public void Type_WithAColorThatIsNotRRGGBB_IsAValidationError(string color)
    {
        Assert.Throws<DomainValidationException>(() => WorkItemType.Create(Guid.NewGuid(), "Epic", 3, color));
    }

    [Test]
    public void Type_Recolor_WithAnInvalidColor_KeepsTheOldColor()
    {
        var type = WorkItemType.Create(Guid.NewGuid(), "Epic", 3, "#D81B60");

        Assert.Throws<DomainValidationException>(() => type.Recolor("red"));
        Assert.That(type.Color, Is.EqualTo("#D81B60"));
    }
}
