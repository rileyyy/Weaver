using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class WorkItemUrlLinkTests
{
    private readonly Guid _item = Guid.NewGuid();

    [Test]
    public void Create_TrimsTheUrlAndLabel()
    {
        var link = WorkItemUrlLink.Create(_item, "  https://github.com/pr/1  ", "  The PR  ");

        Assert.Multiple(() =>
        {
            Assert.That(link.WorkItemId, Is.EqualTo(_item));
            Assert.That(link.Url, Is.EqualTo("https://github.com/pr/1"));
            Assert.That(link.Label, Is.EqualTo("The PR"));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Create_WithABlankLabel_HasNoLabel(string? label)
    {
        var link = WorkItemUrlLink.Create(_item, "http://example.com", label);

        Assert.That(link.Label, Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("example.com")]
    [TestCase("/relative/path")]
    [TestCase("ftp://example.com/file")]
    [TestCase("javascript:alert(1)")]
    [TestCase("mailto:someone@example.com")]
    [TestCase("http:///x")]
    public void Create_WithAnythingButAnAbsoluteWebAddress_IsRejected(string? url)
    {
        Assert.Throws<DomainValidationException>(() => WorkItemUrlLink.Create(_item, url!, null));
    }

    [Test]
    public void Create_WithAUrlAtTheMaximumLength_IsAccepted()
    {
        var url = "https://example.com/" + new string('a', WorkItemUrlLink.UrlMaxLength - "https://example.com/".Length);

        Assert.That(WorkItemUrlLink.Create(_item, url, null).Url, Has.Length.EqualTo(WorkItemUrlLink.UrlMaxLength));
    }

    [Test]
    public void Create_WithAUrlOverTheMaximumLength_IsRejected()
    {
        var url = "https://example.com/" + new string('a', WorkItemUrlLink.UrlMaxLength);

        Assert.Throws<DomainValidationException>(() => WorkItemUrlLink.Create(_item, url, null));
    }

    [Test]
    public void Create_WithALabelOverTheMaximumLength_IsRejected()
    {
        var label = new string('a', WorkItemUrlLink.LabelMaxLength + 1);

        Assert.Throws<DomainValidationException>(() => WorkItemUrlLink.Create(_item, "https://example.com", label));
    }
}
