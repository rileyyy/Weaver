using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Weaver.Infrastructure;

public static class DatabaseMigrations
{
    /// <summary>
    /// Command-line argument that applies pending migrations and exits, for deployments that
    /// want schema changes as one explicit step before the API starts.
    /// </summary>
    public const string Command = "migrate";

    /// <summary>
    /// Whether the API applies pending migrations when it starts. On unless set to
    /// <c>false</c> (<c>Database__MigrateOnStartup=false</c>), which a deployment that runs
    /// <see cref="Command"/> as its own step can use.
    /// </summary>
    public const string MigrateOnStartupSetting = "Database:MigrateOnStartup";

    /// <summary>
    /// Applies pending migrations. Any failure is rethrown as one error that names the cause,
    /// so the process stops instead of going on to serve against an out-of-date schema.
    /// </summary>
    public static async Task MigrateWeaverDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        try
        {
            await scope.ServiceProvider.GetRequiredService<WeaverDbContext>().Database.MigrateAsync(ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"Applying database migrations failed, so the API will not start against an out-of-date schema: {error.Message}",
                error);
        }
    }
}
