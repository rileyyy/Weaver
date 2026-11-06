using Weaver.Application.Auth;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Tests;

/// <summary>Background work, outside any request: reading the caller throws, as it does there.</summary>
public sealed class NoCallerContext : ICallerContext
{
    public Caller Current => throw new InvalidAccessTokenException();
}
