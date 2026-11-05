namespace Weaver.Application.Services;

/// <summary>
/// Names by id, as <see cref="IWorkItemNameLookup"/> found them. An id whose row is gone has no
/// name, so each lookup can return null.
/// </summary>
public sealed class WorkItemNames
{
    public static WorkItemNames None { get; } = new();

    public IReadOnlyDictionary<Guid, string> Spaces { get; init; } = new Dictionary<Guid, string>();

    public IReadOnlyDictionary<Guid, string> Statuses { get; init; } = new Dictionary<Guid, string>();

    public IReadOnlyDictionary<Guid, string> Types { get; init; } = new Dictionary<Guid, string>();

    public IReadOnlyDictionary<Guid, string> Priorities { get; init; } = new Dictionary<Guid, string>();

    public IReadOnlyDictionary<Guid, string> Usernames { get; init; } = new Dictionary<Guid, string>();

    public IReadOnlyDictionary<Guid, WorkItemReference> WorkItems { get; init; } = new Dictionary<Guid, WorkItemReference>();

    public string? SpaceName(Guid id) => Spaces.GetValueOrDefault(id);

    public string? StatusName(Guid id) => Statuses.GetValueOrDefault(id);

    public string? TypeName(Guid? id) => id is { } typeId ? Types.GetValueOrDefault(typeId) : null;

    public string? PriorityName(Guid? id) => id is { } priorityId ? Priorities.GetValueOrDefault(priorityId) : null;

    public string? Username(Guid? id) => id is { } userId ? Usernames.GetValueOrDefault(userId) : null;

    public WorkItemReference? WorkItem(Guid? id) => id is { } workItemId ? WorkItems.GetValueOrDefault(workItemId) : null;
}
