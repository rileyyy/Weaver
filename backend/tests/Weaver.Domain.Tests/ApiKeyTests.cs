using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class ApiKeyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void Issue_ForAnAgentAccount_KeepsTheTrimmedLabelScopeAndHash()
    {
        var agent = User.CreateAgent("Nightly triage", null);

        var key = ApiKey.Issue(agent, "  laptop  ", ApiKeyScope.ReadWrite, "HASH");

        Assert.Multiple(() =>
        {
            Assert.That(key.UserId, Is.EqualTo(agent.Id));
            Assert.That(key.Label, Is.EqualTo("laptop"));
            Assert.That(key.Scope, Is.EqualTo(ApiKeyScope.ReadWrite));
            Assert.That(key.KeyHash, Is.EqualTo("HASH"));
            Assert.That(key.IsRevoked, Is.False);
        });
    }

    [Test]
    public void Issue_ForAHumanAccount_IsRefused()
    {
        var human = User.CreateHuman("alice", "alice");

        Assert.Throws<NotAnAgentAccountException>(() => ApiKey.Issue(human, "laptop", ApiKeyScope.ReadOnly, "HASH"));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Issue_WithoutALabel_IsRefused(string label)
    {
        var agent = User.CreateAgent("Nightly triage", null);

        Assert.Throws<DomainValidationException>(() => ApiKey.Issue(agent, label, ApiKeyScope.ReadOnly, "HASH"));
    }

    [Test]
    public void Revoke_KeepsTheFirstRevocationTime()
    {
        var key = ApiKey.Issue(User.CreateAgent("Nightly triage", null), "laptop", ApiKeyScope.ReadOnly, "HASH");

        key.Revoke(Now);
        key.Revoke(Now.AddHours(1));

        Assert.Multiple(() =>
        {
            Assert.That(key.IsRevoked, Is.True);
            Assert.That(key.RevokedAtUtc, Is.EqualTo(Now));
        });
    }

    // Every request through a key records its use, so the write is skipped while the last one
    // is still recent enough to show.
    [Test]
    public void RecordUse_OnlyMovesLastUsedOnceThePreviousUseIsAMinuteOld()
    {
        var key = ApiKey.Issue(User.CreateAgent("Nightly triage", null), "laptop", ApiKeyScope.ReadOnly, "HASH");

        var first = key.RecordUse(Now);
        var soonAfter = key.RecordUse(Now.AddSeconds(59));
        var aMinuteLater = key.RecordUse(Now.AddMinutes(1));

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(soonAfter, Is.False);
            Assert.That(aMinuteLater, Is.True);
            Assert.That(key.LastUsedAtUtc, Is.EqualTo(Now.AddMinutes(1)));
        });
    }
}
