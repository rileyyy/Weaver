using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Weaver.Api.Auth;
using Weaver.Application.Auth;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Tests;

[TestFixture]
public class HttpCallerContextTests
{
    [Test]
    public void Current_ForASignedInUser_IsTheirAccountWithNoKeyOrActorLabel()
    {
        var id = Guid.NewGuid();

        var caller = ContextFor(SignedIn(new Claim(JwtRegisteredClaimNames.Sub, id.ToString()))).Current;

        Assert.That(caller, Is.EqualTo(new Caller(id, ApiKeyId: null, ActorLabel: null)));
    }

    [Test]
    public void Current_ForAnAgentSignedInWithAKey_CarriesTheKey()
    {
        var accountId = Guid.NewGuid();
        var keyId = Guid.NewGuid();

        var caller = ContextFor(SignedIn(
            new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()),
            new Claim(WeaverClaimTypes.ApiKeyId, keyId.ToString()))).Current;

        Assert.That(caller, Is.EqualTo(new Caller(accountId, ApiKeyId: keyId, ActorLabel: null)));
    }

    [Test]
    public void Current_ForAnAnonymousRequest_ThrowsInvalidAccessToken()
    {
        var context = new HttpCallerContext(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        Assert.Throws<InvalidAccessTokenException>(() => _ = context.Current);
    }

    [Test]
    public void Current_WithoutAUsableSubClaim_ThrowsInvalidAccessToken()
    {
        var context = ContextFor(SignedIn(new Claim(JwtRegisteredClaimNames.Sub, "not-a-guid")));

        Assert.Throws<InvalidAccessTokenException>(() => _ = context.Current);
    }

    [Test]
    public void Current_OutsideAnHttpRequest_ThrowsInvalidAccessToken()
    {
        var context = new HttpCallerContext(new HttpContextAccessor());

        Assert.Throws<InvalidAccessTokenException>(() => _ = context.Current);
    }

    private static ClaimsPrincipal SignedIn(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    private static HttpCallerContext ContextFor(ClaimsPrincipal user) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } });
}
