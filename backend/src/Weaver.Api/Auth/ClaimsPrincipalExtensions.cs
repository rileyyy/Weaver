using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
}
