using Weaver.Infrastructure;

namespace Weaver.Api.Startup;

/// <summary>
/// Decides from the command line and configuration whether the API applies migrations before
/// it serves. Migrating on startup is the default because a deployment runs a single backend
/// instance, so there are no replicas to race (ADR-0008).
/// </summary>
public static class StartupMigrationPolicy
{
    public static StartupMigrationMode Decide(IReadOnlyCollection<string> args, IConfiguration configuration)
    {
        if (args.Contains(DatabaseMigrations.Command))
        {
            return StartupMigrationMode.MigrateAndExit;
        }

        var migrateOnStartup = configuration.GetValue<bool?>(DatabaseMigrations.MigrateOnStartupSetting) ?? true;
        return migrateOnStartup ? StartupMigrationMode.MigrateThenServe : StartupMigrationMode.Skip;
    }
}
