using System.Net;
using Microsoft.AspNetCore.Hosting;
using Weaver.Api.Contracts;

namespace Weaver.Api.IntegrationTests;

[TestFixture]
public class RateLimitingTests
{
    [Test]
    public async Task AuthEndpoints_BeyondThePermitLimit_Return429WithRetryAfter()
    {
        await using var factory = IntegrationTestEnvironment.Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:Auth:PermitLimit", "2"));
        var client = factory.CreateClient();
        var login = new LoginRequest("nobody-here", "wrong password entirely");

        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i < 3; i++)
        {
            responses.Add(await client.PostJsonAsync("/api/auth/login", login));
        }

        Assert.Multiple(() =>
        {
            Assert.That(responses[0].StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(responses[2].StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
            Assert.That(responses[2].Headers.RetryAfter, Is.Not.Null);
        });
    }
}
