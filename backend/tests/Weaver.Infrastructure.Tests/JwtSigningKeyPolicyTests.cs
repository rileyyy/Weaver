using Weaver.Infrastructure.Auth;

namespace Weaver.Infrastructure.Tests;

[TestFixture]
public class JwtSigningKeyPolicyTests
{
    private const string StrongKey = "a-sufficiently-long-production-signing-key";

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void MissingKey_IsRejected(string? key)
    {
        Assert.Throws<InvalidOperationException>(() => JwtSigningKeyPolicy.EnsureUsable(key, isDevelopment: true));
    }

    [Test]
    public void ShortKey_IsRejectedEvenInDevelopment()
    {
        Assert.Throws<InvalidOperationException>(() => JwtSigningKeyPolicy.EnsureUsable("changeme", isDevelopment: true));
    }

    [Test]
    public void DevelopmentKey_IsAllowedOnlyInDevelopment()
    {
        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() =>
                JwtSigningKeyPolicy.EnsureUsable(JwtSigningKeyPolicy.DevelopmentKey, isDevelopment: true));
            Assert.Throws<InvalidOperationException>(() =>
                JwtSigningKeyPolicy.EnsureUsable(JwtSigningKeyPolicy.DevelopmentKey, isDevelopment: false));
        });
    }

    [Test]
    public void StrongKey_IsAccepted()
    {
        Assert.DoesNotThrow(() => JwtSigningKeyPolicy.EnsureUsable(StrongKey, isDevelopment: false));
    }
}
