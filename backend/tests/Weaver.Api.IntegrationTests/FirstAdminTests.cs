using Weaver.Api.Contracts;

namespace Weaver.Api.IntegrationTests;

[TestFixture]
public class FirstAdminTests
{
    [Test]
    public async Task AFreshDatabase_GetsTheConfiguredAdmin_WhoCanSignIn()
    {
        var auth = await (await ApiClient.Anonymous().PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(WeaverApiFactory.FirstAdminUsername, WeaverApiFactory.FirstAdminPassword)))
            .ReadAsync<AuthResponse>();

        Assert.That(auth.User.IsAdmin, Is.True);
    }
}
