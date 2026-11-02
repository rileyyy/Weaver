using Weaver.Domain.Exceptions;

namespace Weaver.Application.Auth;

/// <summary>
/// The caller of the current request, resolved once per request so services can read it
/// instead of each controller and MCP tool passing it along by hand.
/// </summary>
/// <remarks>
/// Mutating services take it as an optional constructor argument for now: nothing reads it
/// yet, and tests that build services directly predate it. Once history records the caller,
/// it should become required.
/// </remarks>
public interface ICallerContext
{
    /// <summary>
    /// The current caller. Throws <see cref="InvalidAccessTokenException"/> when the request
    /// doesn't identify anyone; it's read lazily so work outside a signed-in request (e.g.
    /// background recurrence generation) can still resolve services that depend on it.
    /// </summary>
    Caller Current { get; }
}
