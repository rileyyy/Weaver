using Weaver.Application.Auth;

namespace Weaver.Application.Tests;

/// <summary>A caller a test sets directly, standing in for the signed-in request.</summary>
public sealed class FakeCallerContext : ICallerContext
{
    public FakeCallerContext(Guid accountId, Guid? apiKeyId = null, string? actorLabel = null)
    {
        Current = new Caller(accountId, apiKeyId, actorLabel);
    }

    /// <summary>Someone, for tests that don't care who.</summary>
    public static FakeCallerContext Anyone() => new(Guid.NewGuid());

    public Caller Current { get; set; }
}
