namespace Weaver.Runner.WeaverApi;

/// <summary>A quick pick (e.g. <c>NextWeek</c>) with its dates for today in the server's time zone.</summary>
public sealed record QuickPickRange(string Pick, DateOnly StartDate, DateOnly EndDate);
