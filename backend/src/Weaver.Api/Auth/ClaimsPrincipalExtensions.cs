using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Weaver.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The authenticated user's id, from the access token's `sub` claim.
    /// Throws if called on an unauthenticated principal — every caller sits
    /// behind <c>[Authorize]</c>, so a missing/malformed claim here is a bug,
    /// not a normal "not logged in" case to swallow.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Access token is missing a 'sub' claim.");
        return Guid.Parse(value);
    }
}
