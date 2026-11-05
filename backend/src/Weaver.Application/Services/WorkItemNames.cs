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

    public string? TypeName(Guid? id) => Find(Types, id);

    public string? PriorityName(Guid? id) => Find(Priorities, id);

    public string? Username(Guid? id) => Find(Usernames, id);

    public WorkItemReference? WorkItem(Guid? id) => Find(WorkItems, id);

    private static T? Find<T>(IReadOnlyDictionary<Guid, T> byId, Guid? id)
        where T : class =>
        id is { } key ? byId.GetValueOrDefault(key) : null;
}
