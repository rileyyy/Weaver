using Weaver.Runner.Configuration;
using Weaver.Runner.Scheduling;
using Weaver.Runner.Tasks;

namespace Weaver.Runner.Tests.Fakes;

internal static class TestConfigs
{
    public static TaskConfig WhenResolution(string? cron = null) => new(
        TaskKind.WhenResolution,
        new Uri("http://ollama.test/v1/"),
        "llama3.1:8b",
        null,
        new Uri("http://weaver.test/"),
        "wvr_test",
        cron is null ? null : CronSchedule.Parse(cron, TimeZoneInfo.Utc),
        JsonMode: true,
        TimeSpan.FromMinutes(5));
}
