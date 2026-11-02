using Weaver.Domain.Exceptions;

namespace Weaver.Application.Auth;

/// <summary>
/// The caller of the current request, resolved once per request so services can read it
/// instead of each controller and MCP tool passing it along by hand.
/// </summary>
/// <remarks>
/// Every service that changes a work item takes it as a required constructor argument, since
/// the history it records names the caller. Work with no caller (background recurrence
/// generation) records as <see cref="Domain.HistoryActor.System"/> and never reads it.
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
