using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class UserCredentialsTests
{
    [Test]
    public void NormalizeUsername_TrimsAndLowercases()
    {
        var (username, normalized) = UserCredentials.NormalizeUsername("  Alice.B ");

        Assert.Multiple(() =>
        {
            Assert.That(username, Is.EqualTo("Alice.B"));
            Assert.That(normalized, Is.EqualTo("alice.b"));
        });
    }

    [TestCase("ab")]
    [TestCase("has space")]
    [TestCase("bang!")]
    public void NormalizeUsername_RejectsInvalidNames(string username)
    {
        Assert.Throws<InvalidUsernameException>(() => UserCredentials.NormalizeUsername(username));
    }

    [Test]
    public void ValidatePassword_EnforcesTheLengthLimits()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidPasswordException>(() =>
                UserCredentials.ValidatePassword(new string('x', UserCredentials.MinPasswordLength - 1)));
            Assert.Throws<InvalidPasswordException>(() =>
                UserCredentials.ValidatePassword(new string('x', UserCredentials.MaxPasswordLength + 1)));
            Assert.DoesNotThrow(() => UserCredentials.ValidatePassword(new string('x', UserCredentials.MinPasswordLength)));
        });
    }
}
