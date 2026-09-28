namespace Weaver.Domain.Tests;

[TestFixture]
public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void RecordFailedLogin_LocksOnlyOnTheFifthConsecutiveFailure()
    {
        var user = User.CreateHuman("alice", "alice");

        for (var i = 1; i < User.MaxFailedLoginAttempts; i++)
        {
            user.RecordFailedLogin(Now);
        }

        Assert.That(user.IsLockedOutAt(Now), Is.False);
        user.RecordFailedLogin(Now);
        Assert.That(user.IsLockedOutAt(Now), Is.True);
    }

    [Test]
    public void Lockout_EndsAfterTheLockoutDuration()
    {
        var user = LockedOutUser();

        Assert.Multiple(() =>
        {
            Assert.That(user.IsLockedOutAt(Now + User.LockoutDuration - TimeSpan.FromSeconds(1)), Is.True);
            Assert.That(user.IsLockedOutAt(Now + User.LockoutDuration), Is.False);
        });
    }

    [Test]
    public void RecordSuccessfulLogin_ClearsFailuresAndLockout()
    {
        var user = LockedOutUser();

        user.RecordSuccessfulLogin();

        Assert.Multiple(() =>
        {
            Assert.That(user.FailedLoginAttempts, Is.Zero);
            Assert.That(user.IsLockedOutAt(Now), Is.False);
        });
    }

    [Test]
    public void CreateHuman_IsNotAnAdminUnlessAsked()
    {
        Assert.Multiple(() =>
        {
            Assert.That(User.CreateHuman("alice", "alice").IsAdmin, Is.False);
            Assert.That(User.CreateHuman("alice", "alice", isAdmin: true).IsAdmin, Is.True);
        });
    }

    [Test]
    public void GrantAndRevokeAdmin_ToggleIsAdmin()
    {
        var user = User.CreateHuman("alice", "alice");

        user.GrantAdmin();
        Assert.That(user.IsAdmin, Is.True);

        user.RevokeAdmin();
        Assert.That(user.IsAdmin, Is.False);
    }

    [Test]
    public void Deactivate_KeepsTheFirstDeactivationTime()
    {
        var user = User.CreateHuman("alice", "alice");

        user.Deactivate(Now);
        user.Deactivate(Now.AddHours(1));

        Assert.Multiple(() =>
        {
            Assert.That(user.IsActive, Is.False);
            Assert.That(user.DeactivatedAtUtc, Is.EqualTo(Now));
        });
    }

    [Test]
    public void Reactivate_MakesTheUserActiveAgain()
    {
        var user = User.CreateHuman("alice", "alice");
        user.Deactivate(Now);

        user.Reactivate();

        Assert.That(user.IsActive, Is.True);
    }

    [Test]
    public void ClearLockout_UnlocksImmediately()
    {
        var user = LockedOutUser();

        user.ClearLockout();

        Assert.Multiple(() =>
        {
            Assert.That(user.IsLockedOutAt(Now), Is.False);
            Assert.That(user.FailedLoginAttempts, Is.Zero);
        });
    }

    private static User LockedOutUser()
    {
        var user = User.CreateHuman("alice", "alice");
        for (var i = 0; i < User.MaxFailedLoginAttempts; i++)
        {
            user.RecordFailedLogin(Now);
        }

        return user;
    }
}
