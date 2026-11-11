namespace Weaver.Runner;

public enum RunnerMode
{
    /// <summary>Stay running and start each task kind on its own cron schedule.</summary>
    Schedule,

    /// <summary>Run one task kind once and exit, for testing and outside schedulers.</summary>
    RunOnce,

    Help,

    Invalid,
}
