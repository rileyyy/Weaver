using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Weaver.Api.Auth;
using Weaver.Application.Auth;
using Weaver.Domain;
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
    public void Current_ForAnAgentSignedInWithAKey_CarriesTheKeyAndItsScope()
    {
        var accountId = Guid.NewGuid();
        var keyId = Guid.NewGuid();

        var caller = ContextFor(SignedIn(
            new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()),
            new Claim(WeaverClaimTypes.ApiKeyId, keyId.ToString()),
            new Claim(WeaverClaimTypes.ApiKeyScope, "ReadWrite"))).Current;

        Assert.That(caller, Is.EqualTo(new Caller(accountId, ApiKeyId: keyId, ActorLabel: null, ApiKeyScope.ReadWrite)));
    }

    [TestCase(null)]
    [TestCase("Everything")]
    public void Current_ForAKeyWithoutAReadableScope_IsReadOnly(string? scope)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(WeaverClaimTypes.ApiKeyId, Guid.NewGuid().ToString()),
        };
        if (scope is not null)
        {
            claims.Add(new Claim(WeaverClaimTypes.ApiKeyScope, scope));
        }

        var caller = ContextFor(SignedIn([.. claims])).Current;

        Assert.That(caller.ApiKeyScope, Is.EqualTo(ApiKeyScope.ReadOnly));
    }

    [Test]
    public void Current_WithAnActorLabelHeader_CarriesTheLabel()
    {
        var id = Guid.NewGuid();
        var http = new DefaultHttpContext { User = SignedIn(new Claim(JwtRegisteredClaimNames.Sub, id.ToString())) };
        http.Request.Headers[HttpCallerContext.ActorLabelHeader] = "sub-agent: test-writer";

        var caller = new HttpCallerContext(new HttpContextAccessor { HttpContext = http }).Current;

        Assert.That(caller.ActorLabel, Is.EqualTo("sub-agent: test-writer"));
    }

    [Test]
    public void Current_AfterUseAttribution_PrefersItsLabelOverTheHeader()
    {
        var http = SignedInRequest();
        http.Request.Headers[HttpCallerContext.ActorLabelHeader] = "from the header";
        var context = new HttpCallerContext(new HttpContextAccessor { HttpContext = http });

        context.UseAttribution("from the tool call", runId: null);

        Assert.That(context.Current.ActorLabel, Is.EqualTo("from the tool call"));
    }

    [Test]
    public void Current_WithARunHeader_CarriesTheRunId()
    {
        var http = SignedInRequest();
        http.Request.Headers[HttpCallerContext.RunIdHeader] = "run-42";

        var caller = new HttpCallerContext(new HttpContextAccessor { HttpContext = http }).Current;

        Assert.That(caller.RunId, Is.EqualTo("run-42"));
    }

    [Test]
    public void Current_AfterUseAttribution_PrefersItsRunIdOverTheHeader()
    {
        var http = SignedInRequest();
        http.Request.Headers[HttpCallerContext.RunIdHeader] = "from the header";
        var context = new HttpCallerContext(new HttpContextAccessor { HttpContext = http });

        context.UseAttribution(actorLabel: null, "from the tool call");

        Assert.That(context.Current.RunId, Is.EqualTo("from the tool call"));
    }

    [Test]
    public void Current_AfterUseAttributionWithoutARunId_FallsBackToTheHeader()
    {
        var http = SignedInRequest();
        http.Request.Headers[HttpCallerContext.RunIdHeader] = "set on the connection";
        var context = new HttpCallerContext(new HttpContextAccessor { HttpContext = http });

        context.UseAttribution("sub-agent: test-writer", runId: null);

        Assert.That(context.Current.RunId, Is.EqualTo("set on the connection"));
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

    private static DefaultHttpContext SignedInRequest() =>
        new() { User = SignedIn(new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())) };

    private static ClaimsPrincipal SignedIn(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    private static HttpCallerContext ContextFor(ClaimsPrincipal user) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } });
}
