using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

/// <summary>
/// Works out, for every status, Type and priority of one Space, which of another Space's it
/// becomes in a move: the caller's choice, else the default <see cref="IWorkItemSpaceMoveService"/>
/// states. Every source value is resolved up front, so the mapping doesn't depend on which
/// items happen to be moving.
/// </summary>
internal sealed class SpaceValueMapper
{
    private readonly SpaceLookups _source;
    private readonly SpaceLookups _target;
    private readonly Dictionary<Guid, Guid> _statuses;
    private readonly Dictionary<Guid, Guid?> _types;
    private readonly Dictionary<Guid, Guid?> _priorities;

    private SpaceValueMapper(SpaceLookups source, SpaceLookups target, SpaceMoveMaps maps)
    {
        if (target.Statuses.Count == 0)
        {
            throw new DomainValidationException("The target Space has no statuses to move work items into.");
        }

        _source = source;
        _target = target;
        _statuses = Resolve(source.Statuses, target.Statuses, maps.Statuses.ToDictionary(m => m.Key, m => (Guid?)m.Value), "Status", DefaultStatus)
            .ToDictionary(m => m.Key, m => m.Value!.Value);
        _types = Resolve(source.Types, target.Types, maps.Types, "Type", s => NamedLike(target.Types, s)?.Id);
        _priorities = Resolve(source.Priorities, target.Priorities, maps.Priorities, "Priority", s => NamedLike(target.Priorities, s)?.Id);
    }

    public static async Task<SpaceValueMapper> LoadAsync(
        IWeaverDbContext db,
        Guid sourceSpaceId,
        Guid targetSpaceId,
        SpaceMoveMaps maps,
        CancellationToken ct) =>
        new(await SpaceLookups.LoadAsync(db, sourceSpaceId, ct), await SpaceLookups.LoadAsync(db, targetSpaceId, ct), maps);

    public SpaceMoveValues Map(WorkItem item) => new(
        _target.Statuses.First(s => s.Id == _statuses[item.StatusId]),
        item.TypeId is { } type ? _types[type] : null,
        item.PriorityId is { } priority ? _priorities[priority] : null);

    public SpaceMovePlan Plan(Guid workItemId, Guid targetSpaceId, IReadOnlyList<WorkItem> items) => new(
        workItemId,
        targetSpaceId,
        items.Count,
        items.Count(i => i.IsTrashed),
        items.Sum(i => i.CustomFields.Count),
        Mappings(items.Select(i => (Guid?)i.StatusId), _source.Statuses, _target.Statuses, id => _statuses[id]),
        Mappings(items.Select(i => i.TypeId), _source.Types, _target.Types, id => _types[id]),
        Mappings(items.Select(i => i.PriorityId), _source.Priorities, _target.Priorities, id => _priorities[id]));

    /// <summary>Same name first, so a Space keeping the default To Do / Doing / Done maps one to one.</summary>
    private Guid? DefaultStatus(Status source) =>
        (NamedLike(_target.Statuses, source)
            ?? _target.Statuses.FirstOrDefault(t => t.Category == source.Category)
            ?? _target.Statuses[0]).Id;

    private static T? NamedLike<T>(IReadOnlyList<T> targets, LookupValue source) where T : LookupValue =>
        targets.FirstOrDefault(t => string.Equals(t.Name, source.Name, StringComparison.OrdinalIgnoreCase));

    private static Dictionary<Guid, Guid?> Resolve<T>(
        IReadOnlyList<T> sources,
        IReadOnlyList<T> targets,
        IReadOnlyDictionary<Guid, Guid?> chosen,
        string kind,
        Func<T, Guid?> fallback) where T : LookupValue
    {
        EnsureMapFits(chosen, sources, targets, kind);
        return sources.ToDictionary(s => s.Id, s => chosen.TryGetValue(s.Id, out var target) ? target : fallback(s));
    }

    /// <summary>
    /// A key from the wrong Space is most likely a map written the wrong way round, so it's
    /// refused rather than ignored.
    /// </summary>
    private static void EnsureMapFits<T>(
        IReadOnlyDictionary<Guid, Guid?> chosen,
        IReadOnlyList<T> sources,
        IReadOnlyList<T> targets,
        string kind) where T : LookupValue
    {
        if (chosen.Keys.Where(key => sources.All(s => s.Id != key)).Select(key => (Guid?)key).FirstOrDefault() is { } unknownKey)
        {
            throw new DomainValidationException($"{kind} {unknownKey} isn't one of the moving item's Space's; map from those.");
        }

        if (chosen.Values.Where(value => value is { } id && targets.All(t => t.Id != id)).FirstOrDefault() is { } unknownValue)
        {
            throw new DomainValidationException($"{kind} {unknownValue} isn't one of the target Space's; map to those.");
        }
    }

    private static List<SpaceMoveMapping> Mappings<T>(
        IEnumerable<Guid?> used,
        IReadOnlyList<T> sources,
        IReadOnlyList<T> targets,
        Func<Guid, Guid?> targetOf) where T : LookupValue
    {
        var counts = used.OfType<Guid>().GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        return sources
            .Where(s => counts.ContainsKey(s.Id))
            .Select(s =>
            {
                var targetId = targetOf(s.Id);
                return new SpaceMoveMapping(s.Id, s.Name, targetId, targets.FirstOrDefault(t => t.Id == targetId)?.Name, counts[s.Id]);
            })
            .ToList();
    }
}
