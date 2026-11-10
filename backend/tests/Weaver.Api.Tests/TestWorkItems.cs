using Weaver.Domain;

namespace Weaver.Api.Tests;

/// <summary>Work items for tests that only map or pass them along, in a Space of their own.</summary>
internal static class TestWorkItems
{
    public static readonly DateOnly Today = new(2026, 11, 10);

    public static Status StatusOf(StatusCategory category) =>
        Status.Create(Guid.NewGuid(), category.ToString(), 0, category, "#1E88E5");

    public static WorkItem Create(string title, StatusCategory category = StatusCategory.ToDo) =>
        WorkItem.Create(Guid.NewGuid(), title, null, StatusOf(category), rank: 1.0, Today);
}
