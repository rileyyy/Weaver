namespace Weaver.Runner.WeaverApi;

/// <summary>One page of <c>GET /api/work-items/search</c>.</summary>
internal sealed record WeaverWorkItemPage(IReadOnlyList<WeaverWorkItem> Items, string? NextCursor);
