using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Weaver.Api.Tests;

public static class SignedInControllerContext
{
    /// <summary>A controller context whose user carries <paramref name="userId"/> as the token's <c>sub</c>.</summary>
    public static ControllerContext For(Guid userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId.ToString())], authenticationType: "Test")),
        },
    };
}
