namespace Weaver.Domain.Tests;

/// <summary>A Space's default status of each category, for tests that need a status entity.</summary>
internal static class TestStatuses
{
    public static Status ToDo(Guid spaceId) => Of(spaceId, StatusCategory.ToDo);

    public static Status Doing(Guid spaceId) => Of(spaceId, StatusCategory.Doing);

    public static Status Done(Guid spaceId) => Of(spaceId, StatusCategory.Done);

    private static Status Of(Guid spaceId, StatusCategory category) =>
        SpaceDefaults.StatusesFor(spaceId).Single(s => s.Category == category);
}
