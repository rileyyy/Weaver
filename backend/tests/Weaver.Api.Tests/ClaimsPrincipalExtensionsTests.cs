using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Weaver.Api.Auth;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Tests;

[TestFixture]
public class ClaimsPrincipalExtensionsTests
{
    [Test]
    public void GetUserId_ReadsTheSubClaim()
    {
        var id = Guid.NewGuid();

        Assert.That(Principal(id.ToString()).GetUserId(), Is.EqualTo(id));
    }

    [TestCase(null)]
    [TestCase("not-a-guid")]
    public void GetUserId_WithoutAUsableSubClaim_ThrowsInvalidAccessToken(string? sub)
    {
        Assert.Throws<InvalidAccessTokenException>(() => Principal(sub).GetUserId());
    }

    private static ClaimsPrincipal Principal(string? sub) =>
        new(new ClaimsIdentity(
            sub is null ? [] : [new Claim(JwtRegisteredClaimNames.Sub, sub)],
            authenticationType: "Test"));
}
