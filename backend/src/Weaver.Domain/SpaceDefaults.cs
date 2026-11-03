namespace Weaver.Domain;

/// <summary>
/// The statuses and priorities a new Space starts with, so it can hold work items at once.
/// Admins rename, recolour, reorder or replace them per Space afterwards.
/// </summary>
public static class SpaceDefaults
{
    public static IReadOnlyList<Status> StatusesFor(Guid spaceId) =>
    [
        Status.Create(spaceId, "To Do", 0, StatusCategory.ToDo, "#1E88E5"),
        Status.Create(spaceId, "Doing", 1, StatusCategory.Doing, "#FB8C00"),
        Status.Create(spaceId, "Done", 2, StatusCategory.Done, "#7B1FA2"),
    ];

    public static IReadOnlyList<WorkItemPriority> PrioritiesFor(Guid spaceId) =>
    [
        WorkItemPriority.Create(spaceId, "Low", 0),
        WorkItemPriority.Create(spaceId, "Medium", 1),
        WorkItemPriority.Create(spaceId, "High", 2),
        WorkItemPriority.Create(spaceId, "Urgent", 3),
    ];
}
