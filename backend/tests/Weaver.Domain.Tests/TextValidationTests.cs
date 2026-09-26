using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class TextValidationTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void RequireText_WithMissingOrBlankValue_Throws(string? value)
    {
        var thrown = Assert.Throws<DomainValidationException>(() => TextValidation.RequireText(value, "Title", 10));

        Assert.That(thrown!.Message, Is.EqualTo("Title is required."));
    }

    [Test]
    public void RequireText_OverTheMaxLength_Throws()
    {
        var thrown = Assert.Throws<DomainValidationException>(() =>
            TextValidation.RequireText(new string('a', 11), "Title", 10));

        Assert.That(thrown!.Message, Is.EqualTo("Title must be at most 10 characters."));
    }

    [Test]
    public void RequireText_AtExactlyTheMaxLength_Passes()
    {
        Assert.DoesNotThrow(() => TextValidation.RequireText(new string('a', 10), "Title", 10));
    }

    [Test]
    public void RequireMaxLength_AllowsAnEmptyValue()
    {
        Assert.DoesNotThrow(() => TextValidation.RequireMaxLength("", "Description", 10));
    }
}
