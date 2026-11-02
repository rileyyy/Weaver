using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Weaver.Infrastructure;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// The API migrates its database on startup unless told not to, and refuses to start when
/// migrating fails. Each test gets its own empty database in the shared container, because
/// the shared one is already migrated.
/// </summary>
[TestFixture]
public class StartupMigrationTests
{
    [Test]
    public async Task ByDefault_StartupAppliesEveryMigration()
    {
        var connectionString = await CreateEmptyDatabaseAsync();
        using var factory = Host(connectionString, migrateOnStartup: null);

        var health = await factory.CreateClient().GetAsync("/health");
        var pending = await PendingMigrationsAsync(factory);

        Assert.Multiple(() =>
        {
            Assert.That(health.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(pending, Is.Empty);
        });
    }

    [Test]
    public async Task TurnedOff_StartupLeavesTheSchemaAlone()
    {
        var connectionString = await CreateEmptyDatabaseAsync();
        using var factory = Host(connectionString, migrateOnStartup: "false");

        factory.CreateClient();

        Assert.That(await PendingMigrationsAsync(factory), Is.Not.Empty);
    }

    [Test]
    public void FailedMigration_StopsStartupWithAClearError()
    {
        // Nothing listens on port 1, so migrating fails at the connection.
        using var factory = Host(
            "Host=127.0.0.1;Port=1;Database=weaver;Username=weaver;Password=unused;Timeout=5",
            migrateOnStartup: null);

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.That(error!.Message, Does.Contain("Applying database migrations failed"));
    }

    private static WebApplicationFactory<Program> Host(string connectionString, string? migrateOnStartup) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Weaver", connectionString);
            builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-that-is-long-enough");
            if (migrateOnStartup is not null)
            {
                builder.UseSetting(DatabaseMigrations.MigrateOnStartupSetting, migrateOnStartup);
            }
        });

    private static async Task<string> CreateEmptyDatabaseAsync()
    {
        var shared = new NpgsqlConnectionStringBuilder(IntegrationTestEnvironment.Factory.ConnectionString);
        var name = $"startup_{Guid.NewGuid():N}";

        await using (var connection = new NpgsqlConnection(shared.ConnectionString))
        {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await create.ExecuteNonQueryAsync();
        }

        shared.Database = name;
        return shared.ConnectionString;
    }

    private static async Task<IReadOnlyList<string>> PendingMigrationsAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaverDbContext>();
        return (await db.Database.GetPendingMigrationsAsync()).ToList();
    }
}
