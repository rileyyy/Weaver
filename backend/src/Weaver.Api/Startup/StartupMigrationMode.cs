namespace Weaver.Api.Startup;

/// <summary>What the API does with pending database migrations when the process starts.</summary>
public enum StartupMigrationMode
{
    /// <summary>Serve without touching the schema; the deployment migrates some other way.</summary>
    Skip,

    MigrateThenServe,

    /// <summary>The explicit <c>migrate</c> step.</summary>
    MigrateAndExit,
}
