using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Weaver.Application.Auth;

namespace Weaver.Api.Auth;

/// <summary>
/// Signs an agent in from <c>Authorization: Bearer wvr_…</c> (ADR-0004). The principal carries
/// the same <c>sub</c> claim an access token would, so everything downstream of authentication
/// treats both alike, plus the key's id and scope.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";

    private readonly IApiKeyAuthenticator _authenticator;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyAuthenticator authenticator)
        : base(options, logger, encoder)
    {
        _authenticator = authenticator;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var key = BearerValue(Request);
        if (!ApiKeySecret.LooksLikeKey(key))
        {
            return AuthenticateResult.NoResult();
        }

        var signIn = await _authenticator.AuthenticateAsync(key!, Context.RequestAborted);
        if (signIn is null)
        {
            return AuthenticateResult.Fail("The API key is unknown or revoked, or its account is deactivated.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, signIn.AccountId.ToString()),
                new Claim(WeaverClaimTypes.ApiKeyId, signIn.ApiKeyId.ToString()),
                new Claim(WeaverClaimTypes.ApiKeyScope, signIn.Scope.ToString()),
            ],
            SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    /// <summary>The bearer value, if the request has one, so the scheme selector and this handler agree.</summary>
    public static string? BearerValue(HttpRequest request)
    {
        const string bearer = "Bearer ";
        string? header = request.Headers.Authorization;
        return header is not null && header.StartsWith(bearer, StringComparison.OrdinalIgnoreCase)
            ? header[bearer.Length..].Trim()
            : null;
    }
}
