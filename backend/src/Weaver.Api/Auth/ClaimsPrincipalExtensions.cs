using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The authenticated user's id, from the access token's `sub` claim. Every caller sits
    /// behind <c>[Authorize]</c>, so the token's signature is already valid; a missing or
    /// malformed claim still means the token doesn't identify anyone, which is a 401 rather
    /// than a server error.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : throw new InvalidAccessTokenException();

    /// <summary>The API key an agent signed in with, or null for an access token.</summary>
    public static Guid? GetApiKeyId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(WeaverClaimTypes.ApiKeyId), out var id) ? id : null;

    /// <summary>
    /// The scope of the API key an agent signed in with, or null for an access token. A key's
    /// principal that somehow lacks a readable scope is treated as read-only, the safer reading.
    /// </summary>
    public static ApiKeyScope? GetApiKeyScope(this ClaimsPrincipal principal)
    {
        if (principal.GetApiKeyId() is null)
        {
            return null;
        }

        return Enum.TryParse<ApiKeyScope>(principal.FindFirstValue(WeaverClaimTypes.ApiKeyScope), out var scope)
            ? scope
            : ApiKeyScope.ReadOnly;
    }
}
