namespace Weaver.Application.Services;

/// <summary>One page of a <see cref="WorkItemQuery"/>. <see cref="NextCursor"/> is null on the last page.</summary>
public sealed record WorkItemQueryPage<T>(IReadOnlyList<T> Items, string? NextCursor);
