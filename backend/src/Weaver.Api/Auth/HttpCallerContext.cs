using Weaver.Application.Auth;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Auth;

/// <summary>
/// Reads the caller off the authenticated principal. REST controllers and MCP tools run in
/// the same request pipeline, so this one mapping serves both.
/// </summary>
/// <remarks>
/// The actor label comes from the <see cref="ActorLabelHeader"/> header on REST writes. MCP
/// tools take it as an argument instead, because a client sets its headers once per
/// connection, not per call, and pass it on with <see cref="UseActorLabel"/>.
/// </remarks>
public sealed class HttpCallerContext(IHttpContextAccessor httpContextAccessor) : ICallerContext
{
    public const string ActorLabelHeader = "X-Actor-Label";

    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private string? _actorLabel;

    public Caller Current
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext ?? throw new InvalidAccessTokenException();
            var principal = httpContext.User;
            var actorLabel = _actorLabel ?? httpContext.Request.Headers[ActorLabelHeader].FirstOrDefault();
            return new Caller(
                principal.GetUserId(),
                principal.GetApiKeyId(),
                actorLabel,
                principal.GetApiKeyScope());
        }
    }

    /// <summary>Sets the label for the rest of this request, over any header.</summary>
    public void UseActorLabel(string? actorLabel) => _actorLabel = actorLabel;
}
