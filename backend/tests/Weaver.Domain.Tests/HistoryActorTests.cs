using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class HistoryActorTests
{
    [Test]
    public void Create_TrimsTheActorLabel()
    {
        var actor = HistoryActor.Create(Guid.NewGuid(), Guid.NewGuid(), "  sub-agent: test-writer ");

        Assert.That(actor.ActorLabel, Is.EqualTo("sub-agent: test-writer"));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void Create_WithABlankLabel_HasNoLabel(string? label)
    {
        var actor = HistoryActor.Create(Guid.NewGuid(), null, label);

        Assert.That(actor.ActorLabel, Is.Null);
    }

    [Test]
    public void Create_WithATooLongLabel_IsRejected()
    {
        var label = new string('x', HistoryActor.ActorLabelMaxLength + 1);

        Assert.Throws<DomainValidationException>(() => HistoryActor.Create(Guid.NewGuid(), null, label));
    }

    [Test]
    public void Create_TrimsTheRunId()
    {
        var actor = HistoryActor.Create(Guid.NewGuid(), Guid.NewGuid(), null, " nightly-2026-11-11 ");

        Assert.That(actor.RunId, Is.EqualTo("nightly-2026-11-11"));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void Create_WithABlankRunId_HasNoRunId(string? runId)
    {
        var actor = HistoryActor.Create(Guid.NewGuid(), null, null, runId);

        Assert.That(actor.RunId, Is.Null);
    }

    [Test]
    public void Create_WithATooLongRunId_IsRejected()
    {
        var runId = new string('x', HistoryActor.RunIdMaxLength + 1);

        Assert.Throws<DomainValidationException>(() => HistoryActor.Create(Guid.NewGuid(), null, null, runId));
    }

    [Test]
    public void Create_WithoutARunId_HasNone()
    {
        Assert.That(HistoryActor.Create(Guid.NewGuid(), null, "label").RunId, Is.Null);
    }

    [Test]
    public void System_HasNoAccountKeyOrLabel()
    {
        Assert.That(HistoryActor.System, Is.EqualTo(HistoryActor.Create(null, null, null, null)));
    }
}
