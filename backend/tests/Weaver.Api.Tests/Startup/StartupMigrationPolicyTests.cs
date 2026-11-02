using Microsoft.Extensions.Configuration;
using Weaver.Api.Startup;

namespace Weaver.Api.Tests.Startup;

[TestFixture]
public class StartupMigrationPolicyTests
{
    private static IConfiguration Configuration(string? migrateOnStartup = null)
    {
        var settings = new Dictionary<string, string?>();
        if (migrateOnStartup is not null)
        {
            settings["Database:MigrateOnStartup"] = migrateOnStartup;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    [Test]
    public void ByDefault_MigratesThenServes()
    {
        var mode = StartupMigrationPolicy.Decide([], Configuration());

        Assert.That(mode, Is.EqualTo(StartupMigrationMode.MigrateThenServe));
    }

    [TestCase("false")]
    [TestCase("False")]
    public void TurnedOff_ServesWithoutMigrating(string setting)
    {
        var mode = StartupMigrationPolicy.Decide([], Configuration(setting));

        Assert.That(mode, Is.EqualTo(StartupMigrationMode.Skip));
    }

    [TestCase(null)]
    [TestCase("true")]
    [TestCase("false")]
    public void MigrateCommand_MigratesAndExits_WhateverTheSetting(string? setting)
    {
        var mode = StartupMigrationPolicy.Decide(["migrate"], Configuration(setting));

        Assert.That(mode, Is.EqualTo(StartupMigrationMode.MigrateAndExit));
    }

    [Test]
    public void InvalidSetting_RefusesToStart()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            StartupMigrationPolicy.Decide([], Configuration("nope")));

        Assert.That(error!.Message, Does.Contain("Database:MigrateOnStartup"));
    }
}
