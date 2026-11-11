using Weaver.Application.Auth;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Auth;

/// <summary>
/// Reads the caller off the authenticated principal. REST controllers and MCP tools run in
/// the same request pipeline, so this one mapping serves both.
/// </summary>
/// <remarks>
/// The actor label and run id come from the <see cref="ActorLabelHeader"/> and
/// <see cref="RunIdHeader"/> headers on REST writes. MCP tools take them as arguments instead,
/// because a client sets its headers once per connection, not per call, and pass them on with
/// <see cref="UseAttribution"/>. An argument left out falls back to the header, so an MCP
/// client may still set either one for its whole connection.
/// </remarks>
public sealed class HttpCallerContext(IHttpContextAccessor httpContextAccessor) : ICallerContext
{
    public const string ActorLabelHeader = "X-Actor-Label";
    public const string RunIdHeader = "X-Agent-Run";

    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private string? _actorLabel;
    private string? _runId;

    public Caller Current
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext ?? throw new InvalidAccessTokenException();
            var principal = httpContext.User;
            var headers = httpContext.Request.Headers;
            return new Caller(
                principal.GetUserId(),
                principal.GetApiKeyId(),
                _actorLabel ?? headers[ActorLabelHeader].FirstOrDefault(),
                principal.GetApiKeyScope(),
                _runId ?? headers[RunIdHeader].FirstOrDefault());
        }
    }

    /// <summary>Sets the actor label and run id for the rest of this request, over any header.</summary>
    public void UseAttribution(string? actorLabel, string? runId)
    {
        _actorLabel = actorLabel;
        _runId = runId;
    }
}
