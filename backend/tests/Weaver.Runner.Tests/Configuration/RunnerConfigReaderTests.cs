using Microsoft.Extensions.Configuration;
using Weaver.Runner.Configuration;
using Weaver.Runner.Tasks;

namespace Weaver.Runner.Tests.Configuration;

[TestFixture]
public class RunnerConfigReaderTests
{
    private static readonly Dictionary<string, string?> Complete = new()
    {
        ["Tasks:WhenResolution:ModelEndpoint"] = "http://ollama:11434/v1",
        ["Tasks:WhenResolution:Model"] = "llama3.1:8b",
        ["Tasks:WhenResolution:WeaverUrl"] = "http://backend:8080",
        ["Tasks:WhenResolution:AgentApiKey"] = "wvr_abc",
        ["Tasks:WhenResolution:Cron"] = "0 3 * * *",
        ["Tasks:WhenResolution:TimeZone"] = "Europe/Berlin",
    };

    [Test]
    public void ACompleteTask_IsRead_WithDefaultsForTheOptionalSettings()
    {
        var kindConfig = Read(Complete).For(TaskKind.WhenResolution)!;

        Assert.Multiple(() =>
        {
            Assert.That(kindConfig.ModelEndpoint, Is.EqualTo(new Uri("http://ollama:11434/v1/")));
            Assert.That(kindConfig.Model, Is.EqualTo("llama3.1:8b"));
            Assert.That(kindConfig.ModelApiKey, Is.Null);
            Assert.That(kindConfig.WeaverUrl, Is.EqualTo(new Uri("http://backend:8080/")));
            Assert.That(kindConfig.AgentApiKey, Is.EqualTo("wvr_abc"));
            Assert.That(kindConfig.JsonMode, Is.True);
            Assert.That(kindConfig.ModelTimeout, Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(kindConfig.Schedule!.Next(new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero)),
                Is.EqualTo(new DateTimeOffset(2026, 11, 12, 2, 0, 0, TimeSpan.Zero)));
        });
    }

    [Test]
    public void OptionalSettings_CanBeSet()
    {
        var kindConfig = Read(With(new()
        {
            ["Tasks:WhenResolution:ModelApiKey"] = "sk-test",
            ["Tasks:WhenResolution:JsonMode"] = "false",
            ["Tasks:WhenResolution:ModelTimeoutSeconds"] = "30",
        })).For(TaskKind.WhenResolution)!;

        Assert.Multiple(() =>
        {
            Assert.That(kindConfig.ModelApiKey, Is.EqualTo("sk-test"));
            Assert.That(kindConfig.JsonMode, Is.False);
            Assert.That(kindConfig.ModelTimeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
        });
    }

    [Test]
    public void WithoutACron_TheTaskCanOnlyBeRunOnDemand()
    {
        var kindConfig = Read(With(new() { ["Tasks:WhenResolution:Cron"] = null })).For(TaskKind.WhenResolution)!;

        Assert.That(kindConfig.Schedule, Is.Null);
    }

    [Test]
    public void NoTasksSection_ConfiguresNoTasks()
    {
        Assert.That(Read([]).Tasks, Is.Empty);
    }

    [Test]
    public void EveryProblem_IsReported_AtOnce()
    {
        var error = Assert.Throws<RunnerConfigException>(() => Read(new()
        {
            ["Tasks:WhenResolution:ModelEndpoint"] = "ollama:11434",
            ["Tasks:WhenResolution:Cron"] = "every night",
            ["Tasks:WhenResolution:TimeZone"] = "Mars/Olympus",
            ["Tasks:WhenResolution:ModelTimeoutSeconds"] = "0",
            ["Tasks:WhenResolution:JsonMode"] = "maybe",
        }))!;

        Assert.That(error.Problems, Is.EquivalentTo(new[]
        {
            "Tasks:WhenResolution:ModelEndpoint must be an http or https URL",
            "Tasks:WhenResolution:Model is required",
            "Tasks:WhenResolution:WeaverUrl is required",
            "Tasks:WhenResolution:AgentApiKey is required",
            "Tasks:WhenResolution:Cron isn't a valid cron expression: every night",
            "Tasks:WhenResolution:TimeZone isn't a known time zone: Mars/Olympus",
            "Tasks:WhenResolution:ModelTimeoutSeconds must be a whole number above 0",
            "Tasks:WhenResolution:JsonMode must be true or false",
        }));
    }

    [Test]
    public void AnUnknownTaskKind_IsAProblem()
    {
        var error = Assert.Throws<RunnerConfigException>(() => Read(With(new() { ["Tasks:Research:Model"] = "x" })))!;

        Assert.That(error.Problems, Is.EqualTo(new[] { "Tasks:Research isn't a task kind; known: WhenResolution" }));
    }

    private static Dictionary<string, string?> With(Dictionary<string, string?> overrides)
    {
        var settings = new Dictionary<string, string?>(Complete);
        foreach (var (key, value) in overrides)
        {
            settings[key] = value;
        }

        return settings;
    }

    private static RunnerConfig Read(Dictionary<string, string?> settings) =>
        RunnerConfigReader.Read(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
}
