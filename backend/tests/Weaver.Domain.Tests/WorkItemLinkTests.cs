using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class WorkItemLinkTests
{
    private readonly Guid _a = Guid.NewGuid();
    private readonly Guid _b = Guid.NewGuid();

    [Test]
    public void Create_ToItself_IsRejected()
    {
        Assert.Throws<SelfWorkItemLinkException>(() => WorkItemLink.Create(_a, _a, WorkItemLinkRelation.Related));
    }

    [Test]
    public void Create_WithAnUndefinedRelation_IsRejected_NamingEveryRelation()
    {
        var error = Assert.Throws<DomainValidationException>(() => WorkItemLink.Create(_a, _b, (WorkItemLinkRelation)99));

        Assert.That(error!.Message, Does.Contain("Related, Blocks, BlockedBy, Duplicates, DuplicatedBy"));
    }

    [Test]
    public void OtherSideOf_IsSymmetric()
    {
        var link = WorkItemLink.Create(_a, _b, WorkItemLinkRelation.Related);

        Assert.Multiple(() =>
        {
            Assert.That(link.OtherSideOf(_a), Is.EqualTo(_b));
            Assert.That(link.OtherSideOf(_b), Is.EqualTo(_a));
        });
    }

    [TestCase(WorkItemLinkRelation.Related, WorkItemLinkRelation.Related)]
    [TestCase(WorkItemLinkRelation.Blocks, WorkItemLinkRelation.BlockedBy)]
    [TestCase(WorkItemLinkRelation.BlockedBy, WorkItemLinkRelation.Blocks)]
    [TestCase(WorkItemLinkRelation.Duplicates, WorkItemLinkRelation.DuplicatedBy)]
    [TestCase(WorkItemLinkRelation.DuplicatedBy, WorkItemLinkRelation.Duplicates)]
    public void RelationFrom_EachSide_ReadsTheLinkFromThatSide(WorkItemLinkRelation created, WorkItemLinkRelation seenByOther)
    {
        var link = WorkItemLink.Create(_a, _b, created);

        Assert.Multiple(() =>
        {
            Assert.That(link.RelationFrom(_a), Is.EqualTo(created));
            Assert.That(link.RelationFrom(_b), Is.EqualTo(seenByOther));
        });
    }

    [Test]
    public void Create_AsBlockedBy_StoresTheBlockerFirst()
    {
        var link = WorkItemLink.Create(_a, _b, WorkItemLinkRelation.BlockedBy);

        Assert.Multiple(() =>
        {
            Assert.That(link.Type, Is.EqualTo(WorkItemLinkType.Blocks));
            Assert.That(link.WorkItemId, Is.EqualTo(_b));
            Assert.That(link.LinkedWorkItemId, Is.EqualTo(_a));
        });
    }

    [Test]
    public void Create_AsDuplicatedBy_StoresTheDuplicateFirst()
    {
        var link = WorkItemLink.Create(_a, _b, WorkItemLinkRelation.DuplicatedBy);

        Assert.Multiple(() =>
        {
            Assert.That(link.Type, Is.EqualTo(WorkItemLinkType.Duplicates));
            Assert.That(link.WorkItemId, Is.EqualTo(_b));
            Assert.That(link.LinkedWorkItemId, Is.EqualTo(_a));
        });
    }

    [Test]
    public void RelationFrom_AnItemNotOnTheLink_IsRejected()
    {
        var link = WorkItemLink.Create(_a, _b, WorkItemLinkRelation.Related);

        Assert.Throws<ArgumentException>(() => link.RelationFrom(Guid.NewGuid()));
    }

    [Test]
    public void IsReverseBlocksOf_ABlocksLinkTheOtherWay_IsTrue()
    {
        var aBlocksB = WorkItemLink.Create(_a, _b, WorkItemLinkRelation.Blocks);
        var bBlocksA = WorkItemLink.Create(_a, _b, WorkItemLinkRelation.BlockedBy);

        Assert.That(bBlocksA.IsReverseBlocksOf(aBlocksB), Is.True);
    }

    [TestCase(WorkItemLinkRelation.Related)]
    [TestCase(WorkItemLinkRelation.Duplicates)]
    [TestCase(WorkItemLinkRelation.Blocks)]
    public void IsReverseBlocksOf_AnythingElse_IsFalse(WorkItemLinkRelation existing)
    {
        var aBlocksB = WorkItemLink.Create(_a, _b, WorkItemLinkRelation.Blocks);
        var other = WorkItemLink.Create(_a, _b, existing);

        Assert.That(other.IsReverseBlocksOf(aBlocksB), Is.False);
    }
}
