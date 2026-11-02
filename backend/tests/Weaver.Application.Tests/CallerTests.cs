using Weaver.Application.Auth;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Tests;

[TestFixture]
public class CallerTests
{
    [Test]
    public void EnsureCanWrite_RefusesAReadOnlyKey()
    {
        var caller = new Caller(Guid.NewGuid(), Guid.NewGuid(), ActorLabel: null, ApiKeyScope.ReadOnly);

        var ex = Assert.Throws<ReadOnlyApiKeyException>(caller.EnsureCanWrite);

        Assert.That(ex!.Kind, Is.EqualTo(DomainErrorKind.Forbidden));
    }

    [Test]
    public void EnsureCanWrite_AllowsAReadWriteKey()
    {
        var caller = new Caller(Guid.NewGuid(), Guid.NewGuid(), ActorLabel: null, ApiKeyScope.ReadWrite);

        Assert.DoesNotThrow(caller.EnsureCanWrite);
    }

    [Test]
    public void EnsureCanWrite_AllowsAHumanSignedInWithoutAKey()
    {
        var caller = new Caller(Guid.NewGuid(), ApiKeyId: null, ActorLabel: null);

        Assert.DoesNotThrow(caller.EnsureCanWrite);
    }
}
