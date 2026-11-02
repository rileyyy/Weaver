using Weaver.Application.Auth;

namespace Weaver.Application.Tests;

[TestFixture]
public class FirstAdminPasswordPolicyTests
{
    [Test]
    public void DevelopmentPassword_IsAllowedOnlyInDevelopment()
    {
        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() =>
                FirstAdminPasswordPolicy.EnsureUsable(FirstAdminPasswordPolicy.DevelopmentPassword, isDevelopment: true));
            Assert.Throws<InvalidOperationException>(() =>
                FirstAdminPasswordPolicy.EnsureUsable(FirstAdminPasswordPolicy.DevelopmentPassword, isDevelopment: false));
        });
    }

    [TestCase(null)]
    [TestCase("a-production-admin-password")]
    public void AnyOtherPassword_IsLeftToTheBootstrapper(string? password)
    {
        Assert.DoesNotThrow(() => FirstAdminPasswordPolicy.EnsureUsable(password, isDevelopment: false));
    }
}
