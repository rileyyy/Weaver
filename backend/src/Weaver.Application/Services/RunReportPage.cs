namespace Weaver.Application.Services;

/// <summary>
/// One page of runs, newest first. <see cref="NextOffset"/> is the offset the next page starts
/// at, null when this is the last.
/// </summary>
public sealed record RunReportPage(IReadOnlyList<RunReport> Runs, int? NextOffset);
