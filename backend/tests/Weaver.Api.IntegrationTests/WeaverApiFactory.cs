using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// The real API pipeline (auth, CORS, exception mapping, MCP) against a real Postgres, so
/// provider-specific behaviour the EF InMemory tests can't see (FK restrictions, <c>xmin</c>,
/// <c>text[]</c>, identity columns, unique indexes) is exercised too.
/// </summary>
public class WeaverApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string AllowedOrigin = "https://weaver.test";
    public const string FirstAdminUsername = "first-admin";
    public const string FirstAdminPassword = "first-admin-integration-password";

    /// <summary>Small, so a test can go over it without sending megabytes.</summary>
    public const int MaxAttachmentMegabytes = 1;

    private readonly string _connectionString = connectionString;

    public string ConnectionString => _connectionString;

    /// <summary>This host's own folder, so tests can look at the files and the run leaves none behind.</summary>
    public string AttachmentsPath { get; } =
        Path.Combine(Path.GetTempPath(), "weaver-integration-attachments-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Production, so the tests see the same configuration rules a deployment does.
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:Weaver", _connectionString);
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-that-is-long-enough");
        builder.UseSetting("Cors:AllowedOrigins:0", AllowedOrigin);
        // The only way into a fresh database; every other test account is created by this admin.
        builder.UseSetting("FirstAdmin:Username", FirstAdminUsername);
        builder.UseSetting("FirstAdmin:Password", FirstAdminPassword);
        // Every test signs in from the same in-process "IP"; RateLimitingTests checks the limit.
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "100000");
        builder.UseSetting("Attachments:Path", AttachmentsPath);
        builder.UseSetting("Attachments:MaxFileSizeMegabytes", MaxAttachmentMegabytes.ToString());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        DeleteAttachments();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            DeleteAttachments();
        }
    }

    private void DeleteAttachments()
    {
        if (Directory.Exists(AttachmentsPath))
        {
            Directory.Delete(AttachmentsPath, recursive: true);
        }
    }
}
