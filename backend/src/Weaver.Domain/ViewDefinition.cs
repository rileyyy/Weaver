namespace Weaver.Domain;

/// <summary>
/// Everything a <see cref="View"/> stores, as a caller sends it: its name, the work-item query's
/// inputs it keeps and its layout. The time range isn't one of them: a saved date range goes
/// stale, so the window is chosen when the View is shown.
/// </summary>
/// <param name="SpaceIds">The Spaces it shows; empty for every Space.</param>
/// <param name="ApplyDoneHiding">False to show Done items however long ago they were completed.</param>
/// <param name="ScheduledOrInProgressOnly">Overview's rule: only Doing items and dated ones.</param>
public sealed record ViewDefinition(
    string Name,
    IReadOnlyList<Guid> SpaceIds,
    string? Text,
    IReadOnlyList<string> Tags,
    TagMatch TagMatch,
    bool ApplyDoneHiding,
    bool ScheduledOrInProgressOnly,
    ViewLayout Layout);
