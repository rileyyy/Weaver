using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class WorkItemLinkTests
{
    [Test]
    public void Create_ToItself_IsRejected()
    {
        var id = Guid.NewGuid();

        Assert.Throws<SelfWorkItemLinkException>(() => WorkItemLink.Create(id, id));
    }

    [Test]
    public void OtherSideOf_IsSymmetric()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var link = WorkItemLink.Create(a, b);

        Assert.Multiple(() =>
        {
            Assert.That(link.OtherSideOf(a), Is.EqualTo(b));
            Assert.That(link.OtherSideOf(b), Is.EqualTo(a));
        });
    }
}
