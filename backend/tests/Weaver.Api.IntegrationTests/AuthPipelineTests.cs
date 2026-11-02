using System.Net;
using System.Net.Http.Headers;
using Weaver.Api.Contracts;

namespace Weaver.Api.IntegrationTests;

[TestFixture]
public class AuthPipelineTests
{
    [Test]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var response = await ApiClient.Anonymous().GetAsync("/api/work-items");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ProtectedEndpoint_WithForgedToken_Returns401()
    {
        var client = ApiClient.Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.jwt");

        var response = await client.GetAsync("/api/work-items");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task AnAdminCreatedAccount_ThenAuthorizedCall_ResolvesTheUser()
    {
        var (client, auth) = await ApiClient.SignedInAsync();

        var me = await (await client.GetAsync("/api/auth/me")).ReadAsync<UserDto>();

        Assert.That(me.Id, Is.EqualTo(auth.User.Id));
    }

    [Test]
    public async Task Login_WithAnAdminCreatedAccount_ReturnsWorkingTokens()
    {
        var (_, created) = await ApiClient.SignedInAsync();
        var client = ApiClient.Anonymous();

        var login = await (await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(created.User.Username, ApiClient.Password))).ReadAsync<AuthResponse>();
        var refreshed = await (await client.PostJsonAsync(
            "/api/auth/refresh",
            new RefreshRequest(login.RefreshToken))).ReadAsync<AuthResponse>();

        Assert.That(refreshed.User.Id, Is.EqualTo(created.User.Id));
    }

    [Test]
    public async Task SelfRegistration_IsGone()
    {
        // Signed in, because the fallback policy answers any anonymous request with a 401 and
        // would hide whether the route exists.
        var (client, _) = await ApiClient.SignedInAsync();
        var username = ApiClient.UniqueUsername();

        var response = await client.PostJsonAsync("/api/auth/register", new LoginRequest(username, ApiClient.Password));
        var login = await ApiClient.Anonymous().PostJsonAsync("/api/auth/login", new LoginRequest(username, ApiClient.Password));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        });
    }

    [Test]
    public async Task Cors_Preflight_AllowsOnlyTheConfiguredOrigin()
    {
        var client = ApiClient.Anonymous();

        var allowed = await client.SendAsync(Preflight(WeaverApiFactory.AllowedOrigin));
        var other = await client.SendAsync(Preflight("https://evil.test"));

        Assert.Multiple(() =>
        {
            Assert.That(
                allowed.Headers.GetValues("Access-Control-Allow-Origin"),
                Is.EqualTo(new[] { WeaverApiFactory.AllowedOrigin }));
            Assert.That(other.Headers.Contains("Access-Control-Allow-Origin"), Is.False);
        });
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/work-items");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        return request;
    }
}
