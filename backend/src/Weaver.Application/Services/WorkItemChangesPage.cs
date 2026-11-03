namespace Weaver.Application.Services;

/// <summary>One page of changed items. <see cref="NextCursor"/> is null on the last page.</summary>
public sealed record WorkItemChangesPage(IReadOnlyList<ChangedWorkItem> Items, string? NextCursor);
