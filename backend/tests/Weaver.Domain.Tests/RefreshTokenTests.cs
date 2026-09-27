namespace Weaver.Domain.Tests;

[TestFixture]
public class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);

    [Test]
    public void IsActive_UntilItExpires()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", expiresAtUtc: Now.AddDays(1));

        Assert.Multiple(() =>
        {
            Assert.That(token.IsActiveAt(Now), Is.True);
            Assert.That(token.IsActiveAt(Now.AddDays(1)), Is.False);
        });
    }

    [Test]
    public void Revoke_DeactivatesAndKeepsTheFirstRevocationTime()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", Now.AddDays(1));

        token.Revoke(Now);
        token.Revoke(Now.AddMinutes(5));

        Assert.Multiple(() =>
        {
            Assert.That(token.IsActiveAt(Now), Is.False);
            Assert.That(token.RevokedAtUtc, Is.EqualTo(Now));
        });
    }

    [Test]
    public void AReplayWithinTheGracePeriod_IsNotTreatedAsTheft()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", Now.AddDays(1));
        token.RotateTo("next-hash", Now);

        Assert.Multiple(() =>
        {
            Assert.That(token.IsReplayedAt(Now + Grace, Grace), Is.False);
            Assert.That(token.IsReplayedAt(Now + Grace + TimeSpan.FromSeconds(1), Grace), Is.True);
        });
    }

    [Test]
    public void ALoggedOutToken_IsNeverAReplay()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", Now.AddDays(1));
        token.Revoke(Now);

        Assert.That(token.IsReplayedAt(Now.AddHours(1), Grace), Is.False);
    }
}
