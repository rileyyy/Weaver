using Weaver.Application.Auth;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Auth;

/// <summary>
/// Reads the caller off the authenticated principal. REST controllers and MCP tools run in
/// the same request pipeline, so this one mapping serves both.
/// </summary>
public sealed class HttpCallerContext : ICallerContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCallerContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Caller Current
    {
        get
        {
            var principal = _httpContextAccessor.HttpContext?.User ?? throw new InvalidAccessTokenException();
            return new Caller(principal.GetUserId(), ApiKeyId: null, ActorLabel: null);
        }
    }
}
