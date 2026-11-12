namespace Weaver.Runner.WeaverApi;

/// <summary>The parts of a child's <c>WorkItemDto</c> the runner reads.</summary>
public sealed record WeaverSubItem(Guid Id, int Number, string Title, DateTimeOffset CreatedAtUtc);
