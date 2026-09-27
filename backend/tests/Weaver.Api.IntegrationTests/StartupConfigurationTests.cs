using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Weaver.Infrastructure.Auth;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// A deployment that forgets a secret must fail at startup rather than fall back to a value
/// committed to the repository.
/// </summary>
[TestFixture]
public class StartupConfigurationTests
{
    [Test]
    public void Production_WithoutASigningKey_RefusesToStart()
    {
        using var factory = ProductionHost(signingKey: null);

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.That(error!.Message, Does.Contain("Jwt:SigningKey is not configured"));
    }

    [Test]
    public void Production_WithTheDevelopmentKey_RefusesToStart()
    {
        using var factory = ProductionHost(JwtSigningKeyPolicy.DevelopmentKey);

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.That(error!.Message, Does.Contain("public development key"));
    }

    [Test]
    public void Production_WithoutAConnectionString_RefusesToStart()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production"));

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.That(error!.Message, Does.Contain("ConnectionStrings:Weaver"));
    }

    private static WebApplicationFactory<Program> ProductionHost(string? signingKey) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            // Never reached: startup fails before anything connects.
            builder.UseSetting("ConnectionStrings:Weaver", "Host=unused");
            if (signingKey is not null)
            {
                builder.UseSetting("Jwt:SigningKey", signingKey);
            }
        });
}
