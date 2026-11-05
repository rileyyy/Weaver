using Weaver.Application.Auth;

namespace Weaver.Api.IntegrationTests;

/// <summary>For a service built outside a request that only reads, and so never asks who's calling.</summary>
public class NoCaller : ICallerContext
{
    public Caller Current => throw new InvalidOperationException("A read-only test has no caller.");
}
