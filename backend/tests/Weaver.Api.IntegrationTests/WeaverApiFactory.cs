using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// The real API pipeline (auth, CORS, exception mapping, MCP) against a real Postgres, so
/// provider-specific behaviour the EF InMemory tests can't see (FK restrictions, <c>xmin</c>,
/// <c>text[]</c>, identity columns, unique indexes) is exercised too.
/// </summary>
public class WeaverApiFactory : WebApplicationFactory<Program>
{
    public const string AllowedOrigin = "https://weaver.test";
    public const string FirstAdminUsername = "first-admin";
    public const string FirstAdminPassword = "first-admin-integration-password";

    private readonly string _connectionString;

    public WeaverApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public string ConnectionString => _connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Production, so the tests see the same configuration rules a deployment does.
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:Weaver", _connectionString);
        // A fresh container has no schema; production runs the "migrate" step instead.
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-that-is-long-enough");
        builder.UseSetting("Cors:AllowedOrigins:0", AllowedOrigin);
        // The only way into a fresh database; every other test account is created by this admin.
        builder.UseSetting("FirstAdmin:Username", FirstAdminUsername);
        builder.UseSetting("FirstAdmin:Password", FirstAdminPassword);
        // Every test registers from the same in-process "IP"; RateLimitingTests checks the limit.
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "100000");
    }
}
