using System.Globalization;
using Microsoft.Extensions.Configuration;
using Weaver.Runner.Scheduling;
using Weaver.Runner.Tasks;

namespace Weaver.Runner.Configuration;

/// <summary>
/// Reads the <c>Tasks</c> section, one subsection per task kind, from any configuration source
/// (a JSON file, or environment variables such as <c>Tasks__WhenResolution__Model</c>). Every
/// problem is collected and reported together, so a misconfigured runner fails at start with
/// the whole list rather than one problem per restart.
/// </summary>
public static class RunnerConfigReader
{
    private const int DefaultModelTimeoutSeconds = 300;

    public static RunnerConfig Read(IConfiguration configuration)
    {
        var problems = new List<string>();
        var kindConfigs = new List<TaskConfig>();
        foreach (var section in configuration.GetSection("Tasks").GetChildren())
        {
            var kind = TaskKind.FromConfigKey(section.Key);
            if (kind is null)
            {
                problems.Add($"{section.Path} isn't a task kind; known: {string.Join(", ", TaskKind.All.Select(k => k.ConfigKey))}");
                continue;
            }

            if (ReadKind(kind, section, problems) is { } kindConfig)
            {
                kindConfigs.Add(kindConfig);
            }
        }

        return problems.Count > 0 ? throw new RunnerConfigException(problems) : new RunnerConfig(kindConfigs);
    }

    private static TaskConfig? ReadKind(TaskKind kind, IConfigurationSection section, List<string> problems)
    {
        var problemsBefore = problems.Count;
        var modelEndpoint = RequiredUrl(section, "ModelEndpoint", problems);
        var model = Required(section, "Model", problems);
        var weaverUrl = RequiredUrl(section, "WeaverUrl", problems);
        var agentApiKey = Required(section, "AgentApiKey", problems);
        var timeZone = ReadTimeZone(section, problems);
        var schedule = ReadSchedule(section, timeZone, problems);
        var jsonMode = ReadJsonMode(section, problems);
        var timeout = ReadModelTimeout(section, problems);

        return problems.Count > problemsBefore
            ? null
            : new TaskConfig(
                kind,
                modelEndpoint!,
                model!,
                Optional(section, "ModelApiKey"),
                weaverUrl!,
                agentApiKey!,
                schedule,
                jsonMode,
                timeout);
    }

    private static string? Optional(IConfigurationSection section, string key) =>
        string.IsNullOrWhiteSpace(section[key]) ? null : section[key]!.Trim();

    private static string? Required(IConfigurationSection section, string key, List<string> problems)
    {
        var value = Optional(section, key);
        if (value is null)
        {
            problems.Add($"{section.Path}:{key} is required");
        }

        return value;
    }

    /// <summary>
    /// Ends the URL with a slash, so relative paths such as <c>chat/completions</c> are appended
    /// to it rather than replacing its last segment (<c>/v1</c>).
    /// </summary>
    private static Uri? RequiredUrl(IConfigurationSection section, string key, List<string> problems)
    {
        var value = Required(section, key, problems);
        if (value is null)
        {
            return null;
        }

        if (!Uri.TryCreate(value.EndsWith('/') ? value : value + "/", UriKind.Absolute, out var url)
            || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            problems.Add($"{section.Path}:{key} must be an http or https URL");
            return null;
        }

        return url;
    }

    /// <summary>The container's own zone (its <c>TZ</c>) unless one is given.</summary>
    private static TimeZoneInfo ReadTimeZone(IConfigurationSection section, List<string> problems)
    {
        var id = Optional(section, "TimeZone");
        if (id is null)
        {
            return TimeZoneInfo.Local;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            problems.Add($"{section.Path}:TimeZone isn't a known time zone: {id}");
            return TimeZoneInfo.Local;
        }
    }

    private static CronSchedule? ReadSchedule(IConfigurationSection section, TimeZoneInfo timeZone, List<string> problems)
    {
        var expression = Optional(section, "Cron");
        if (expression is null)
        {
            return null;
        }

        if (!CronSchedule.TryParse(expression, timeZone, out var schedule))
        {
            problems.Add($"{section.Path}:Cron isn't a valid cron expression: {expression}");
            return null;
        }

        return schedule;
    }

    private static bool ReadJsonMode(IConfigurationSection section, List<string> problems)
    {
        var value = Optional(section, "JsonMode");
        if (value is null)
        {
            return true;
        }

        if (!bool.TryParse(value, out var jsonMode))
        {
            problems.Add($"{section.Path}:JsonMode must be true or false");
            return true;
        }

        return jsonMode;
    }

    private static TimeSpan ReadModelTimeout(IConfigurationSection section, List<string> problems)
    {
        var value = Optional(section, "ModelTimeoutSeconds");
        if (value is null)
        {
            return TimeSpan.FromSeconds(DefaultModelTimeoutSeconds);
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0)
        {
            problems.Add($"{section.Path}:ModelTimeoutSeconds must be a whole number above 0");
            return TimeSpan.FromSeconds(DefaultModelTimeoutSeconds);
        }

        return TimeSpan.FromSeconds(seconds);
    }
}
