using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Weaver.Infrastructure;

public static class DatabaseMigrations
{
    /// <summary>
    /// Command-line argument that applies pending migrations and exits, so a deployment runs
    /// schema changes as one explicit step instead of every API replica racing to apply them
    /// on boot.
    /// </summary>
    public const string Command = "migrate";

    /// <summary>
    /// Opt-in for applying migrations when the API starts. Convenient in development, where
    /// <c>dotnet watch</c> restarts often and nobody runs a separate step.
    /// </summary>
    public const string MigrateOnStartupSetting = "Database:MigrateOnStartup";

    public static async Task MigrateWeaverDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<WeaverDbContext>().Database.MigrateAsync(ct);
    }
}
