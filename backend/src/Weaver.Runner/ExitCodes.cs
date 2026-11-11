namespace Weaver.Runner;

/// <summary>
/// The process's exit codes, so an outside scheduler (Komodo, cron) can tell a stopped run from
/// a setup mistake. Items skipped within a run still count as <see cref="Success"/>.
/// </summary>
public static class ExitCodes
{
    public const int Success = 0;
    public const int RunStopped = 1;
    public const int BadSetup = 2;
}
