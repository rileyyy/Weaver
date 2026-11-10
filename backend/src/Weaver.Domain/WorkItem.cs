using System.Linq.Expressions;
using System.Text.Json;
using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

/// <summary>
/// A node in a work item tree, inside one <see cref="Space"/>. State changes go through
/// methods so the rules below hold for every caller (REST, MCP, background work), not just the
/// ones a service remembers to check. Rules that need other rows (cycle detection, existence
/// of the new parent, rank neighbours) stay in the service, which then calls these methods.
/// </summary>
public class WorkItem : IHasUpdatedAt
{
    public const int TitleMaxLength = 500;
    public const int MaxTags = 20;
    public const int TagMaxLength = 50;
    public const int WhenMaxLength = 200;

    private List<string> _tags = [];
    private Dictionary<Guid, JsonElement> _customFields = [];

    /// <summary>For EF Core, which materializes through private setters.</summary>
    private WorkItem()
    {
        Title = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// A short, sequential, human-facing identifier (e.g. Azure DevOps'
    /// "#1234") — database-generated (see <c>WorkItemConfiguration</c>),
    /// never set by application code. <see cref="Id"/> remains the real
    /// primary key everywhere else (foreign keys, API routes); this exists
    /// purely for display.
    /// </summary>
    public int Number { get; private set; }

    /// <summary>
    /// The Space the item belongs to. A top-level item is created in a chosen Space and every
    /// descendant shares it; reparenting can't change it, only <see cref="MoveToSpace"/> (ADR-0001).
    /// </summary>
    public Guid SpaceId { get; private set; }

    public Guid? ParentId { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public Guid StatusId { get; private set; }

    /// <summary>
    /// What kind of item this is (e.g. Project/Goal/Task) — a label from the
    /// configurable <see cref="WorkItemType"/> table, not a constraint on
    /// the parent/child tree. Nullable: existing/ad-hoc items don't require
    /// categorization.
    /// </summary>
    public Guid? TypeId { get; private set; }

    /// <summary>
    /// How urgent this item is — a label from the configurable <see cref="WorkItemPriority"/>
    /// table. Nullable like <see cref="TypeId"/>: an item doesn't have to be prioritised.
    /// </summary>
    public Guid? PriorityId { get; private set; }

    public Guid? AssignedToUserId { get; private set; }

    /// <summary>
    /// Short, free-text labels (e.g. "urgent", "needs review") — zero or
    /// more, never null. Replaced as a whole via <see cref="SetTags"/>.
    /// </summary>
    public IReadOnlyList<string> Tags
    {
        get => _tags;
        private set => _tags = [.. value];
    }

    /// <summary>
    /// The values of the item's Type's custom fields, by field id (ADR-0002); a field with no
    /// value has no entry. Stored as one JSON document so adding a field needs no migration.
    /// Changed through <see cref="SetCustomFields"/>, and cleared when the Type changes, since
    /// another Type's fields are different fields.
    /// </summary>
    public IReadOnlyDictionary<Guid, JsonElement> CustomFields
    {
        get => _customFields;
        private set => _customFields = new Dictionary<Guid, JsonElement>(value);
    }

    public double Rank { get; private set; }

    /// <summary>
    /// When this item is scheduled to start/end. Either may be set without
    /// the other — an open start or end is treated as unbounded on that
    /// side by time-frame filters, not as "never scheduled."
    /// </summary>
    public DateOnly? StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    /// <summary>
    /// The free-text intent behind the item's dates ("before the Christmas market", "after
    /// drywall"). Kept as typed after it's resolved, so the phrase still explains the dates.
    /// </summary>
    public string? When { get; private set; }

    /// <summary>
    /// The calendar date (in the server's time zone) the item entered a Done-category status, or
    /// null while its status isn't Done. Kept when it moves between two Done statuses, so it says
    /// when the work was finished rather than when it was last shuffled. Every operation that can
    /// change the status, or what its status means, applies the one rule in <see cref="FollowStatusCategory"/>.
    /// </summary>
    public DateOnly? CompletionDate { get; private set; }

    /// <summary>
    /// Whether the When has been given concrete form: a date, or a live item blocking this one
    /// (ADR-0003). False when there is no When, since there is nothing to resolve. Blockers are
    /// other rows, so the caller says whether there is one; see <see cref="WhenResolutionNeedsBlockers"/>.
    /// </summary>
    public bool IsWhenResolved(bool hasLiveBlocker) =>
        When is not null && (StartDate is not null || EndDate is not null || hasLiveBlocker);

    /// <summary>
    /// <see cref="WhenResolutionNeedsBlockers"/> as a query predicate, so a query listing
    /// unresolved Whens applies the same rule as the items themselves.
    /// </summary>
    public static readonly Expression<Func<WorkItem, bool>> WhenResolutionNeedsBlockersRule =
        w => w.When != null && w.StartDate == null && w.EndDate == null;

    private static readonly Func<WorkItem, bool> WhenResolutionNeedsBlockersCheck = WhenResolutionNeedsBlockersRule.Compile();

    /// <summary>
    /// Whether <see cref="IsWhenResolved"/> depends on blockers at all, so readers can skip
    /// looking them up for every other item. Such an item that nothing live blocks is unresolved.
    /// </summary>
    public bool WhenResolutionNeedsBlockers => WhenResolutionNeedsBlockersCheck(this);

    /// <summary>
    /// The repeating work item this one was generated from, and the occurrence date it was
    /// generated for. Both null for items created by hand. Together they're unique, which is
    /// what stops generation from creating the same occurrence twice.
    /// </summary>
    public Guid? RecurrenceSourceId { get; private set; }

    public DateOnly? RecurrenceDate { get; private set; }

    /// <summary>
    /// When the item went to the Trash (ADR-0007), or null while it's live. Every normal read
    /// leaves trashed items out; the row stays, with its links and comments, until it's purged.
    /// </summary>
    public DateTimeOffset? TrashedAtUtc { get; private set; }

    /// <summary>
    /// The item whose delete trashed this one: itself when it was the one deleted, otherwise
    /// the ancestor whose subtree it went with. Restoring that item brings back exactly the
    /// items trashed with it, not ones deleted separately before.
    /// </summary>
    public Guid? TrashRootId { get; private set; }

    public bool IsTrashed => TrashedAtUtc is not null;

    /// <summary>The item a delete was made on, as opposed to one that went with its ancestor.</summary>
    public bool IsTrashRoot => IsTrashed && TrashRootId == Id;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public Space? Space { get; private set; }

    public WorkItem? Parent { get; private set; }

    public ICollection<WorkItem> Children { get; private set; } = new List<WorkItem>();

    public Status? Status { get; private set; }

    public WorkItemType? Type { get; private set; }

    public WorkItemPriority? Priority { get; private set; }

    public User? AssignedToUser { get; private set; }

    public WorkItem? RecurrenceSource { get; private set; }

    /// <summary>
    /// A top-level item in <paramref name="spaceId"/>. <paramref name="today"/> is the server's
    /// date, which becomes the completion date when <paramref name="status"/> is a Done one.
    /// </summary>
    public static WorkItem Create(
        Guid spaceId,
        string title,
        string? description,
        Status status,
        double rank,
        DateOnly today,
        Guid? typeId = null,
        Guid? priorityId = null) =>
        New(spaceId, null, title, description, status, rank, today, typeId, priorityId);

    /// <summary>A child of <paramref name="parent"/>, in the parent's Space.</summary>
    public static WorkItem CreateChildOf(
        WorkItem parent,
        string title,
        string? description,
        Status status,
        double rank,
        DateOnly today,
        Guid? typeId = null,
        Guid? priorityId = null) =>
        New(parent.SpaceId, parent.Id, title, description, status, rank, today, typeId, priorityId);

    private static WorkItem New(
        Guid spaceId,
        Guid? parentId,
        string title,
        string? description,
        Status status,
        double rank,
        DateOnly today,
        Guid? typeId,
        Guid? priorityId)
    {
        TextValidation.RequireText(title, "Title", TitleMaxLength);

        var item = new WorkItem
        {
            Id = Guid.NewGuid(),
            SpaceId = spaceId,
            ParentId = parentId,
            StatusId = status.Id,
            Rank = rank,
            Title = title,
            Description = description,
            TypeId = typeId,
            PriorityId = priorityId,
        };
        item.FollowStatusCategory(status, today);
        return item;
    }

    /// <summary>
    /// The descriptive fields share no invariant with status, parent, schedule or assignee,
    /// so they are safe to change together. Returns the fields whose value changed.
    /// </summary>
    public IReadOnlyList<FieldChange> UpdateDetails(WorkItemDetailsPatch patch)
    {
        if (patch.Title.HasValue)
        {
            TextValidation.RequireText(patch.Title.Value, "Title", TitleMaxLength);
        }

        var changed = new ChangedFields();
        Title = changed.Apply(WorkItemField.Title, Title, patch.Title);
        Description = changed.Apply(WorkItemField.Description, Description, patch.Description);
        var typeId = changed.Apply(WorkItemField.TypeId, TypeId, patch.TypeId);
        PriorityId = changed.Apply(WorkItemField.PriorityId, PriorityId, patch.PriorityId);
        When = changed.Apply(WorkItemField.When, When, NormalizeWhen(patch.When));
        return [.. changed.Changes, .. SwitchType(typeId)];
    }

    private static Optional<string?> NormalizeWhen(Optional<string?> when)
    {
        if (!when.HasValue)
        {
            return when;
        }

        var trimmed = when.Value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return new Optional<string?>(null);
        }

        TextValidation.RequireMaxLength(trimmed, "When", WhenMaxLength);
        return trimmed;
    }

    /// <summary>Used when an admin deletes the item's type and picks a replacement (or none).</summary>
    public IReadOnlyList<FieldChange> ChangeType(Guid? typeId)
    {
        var changed = new ChangedFields();
        var newTypeId = changed.Set(WorkItemField.TypeId, TypeId, typeId);
        return [.. changed.Changes, .. SwitchType(newTypeId)];
    }

    /// <summary>
    /// Field definitions belong to one Type, so none of the old Type's fields exist on the new
    /// one: their values are cleared, each reported so history keeps what was lost.
    /// </summary>
    private IReadOnlyList<FieldChange> SwitchType(Guid? typeId)
    {
        if (typeId == TypeId)
        {
            return [];
        }

        TypeId = typeId;
        var cleared = _customFields
            .Select(v => new FieldChange(WorkItemField.CustomField(v.Key), v.Value, null))
            .ToList();
        _customFields = [];
        return cleared;
    }

    /// <summary>
    /// Sets the custom field values in <paramref name="patch"/>, leaving every other field's
    /// alone. Returns the fields whose value changed.
    /// </summary>
    public IReadOnlyList<FieldChange> SetCustomFields(CustomFieldPatch patch)
    {
        if (patch.Values.Count > 0 && patch.TypeId != TypeId)
        {
            throw new DomainValidationException("The work item's Type changed; read its fields again.");
        }

        return patch.Values
            .Select(v => SetCustomField(v.Field.Id, v.Value))
            .OfType<FieldChange>()
            .ToList();
    }

    /// <summary>Used when an admin removes a field from the item's Type.</summary>
    public IReadOnlyList<FieldChange> RemoveCustomField(Guid fieldId) =>
        SetCustomField(fieldId, null) is { } change ? [change] : [];

    /// <summary>Used when an admin removes options from a select field: choices of them go.</summary>
    public IReadOnlyList<FieldChange> PruneCustomField(CustomFieldDefinition field) =>
        _customFields.TryGetValue(field.Id, out var stored) && SetCustomField(field.Id, field.Prune(stored)) is { } change
            ? [change]
            : [];

    private FieldChange? SetCustomField(Guid fieldId, JsonElement? value)
    {
        var old = _customFields.TryGetValue(fieldId, out var current) ? current : (JsonElement?)null;
        if (Compact(old) == Compact(value))
        {
            return null;
        }

        if (value is { } next)
        {
            _customFields[fieldId] = next;
        }
        else
        {
            _customFields.Remove(fieldId);
        }

        return new FieldChange(WorkItemField.CustomField(fieldId), old, value);
    }

    /// <summary>
    /// Compared re-serialized rather than by raw text: Postgres returns jsonb with its own
    /// whitespace, which would make an unchanged value look changed.
    /// </summary>
    private static string? Compact(JsonElement? value) => value is { } v ? JsonSerializer.Serialize(v) : null;

    /// <summary>Used when an admin deletes the item's priority and picks a replacement (or none).</summary>
    public IReadOnlyList<FieldChange> ChangePriority(Guid? priorityId)
    {
        var changed = new ChangedFields();
        PriorityId = changed.Set(WorkItemField.PriorityId, PriorityId, priorityId);
        return changed.Changes;
    }

    /// <summary>
    /// Moves the item to another column. Never touches <see cref="ParentId"/>: that is what
    /// guarantees a drag between columns can never reparent the item.
    /// </summary>
    public IReadOnlyList<FieldChange> MoveToStatus(Status status, double rank, DateOnly today)
    {
        var changed = new ChangedFields();
        StatusId = changed.Set(WorkItemField.StatusId, StatusId, status.Id);
        Rank = changed.Set(WorkItemField.Rank, Rank, rank);
        return [.. changed.Changes, .. FollowStatusCategory(status, today)];
    }

    /// <summary>
    /// Used when an admin changes the category of the item's own status: moving it into or out
    /// of Done completes or reopens the item, just as moving the item to such a status would.
    /// </summary>
    public IReadOnlyList<FieldChange> FollowStatusRecategorized(Status status, DateOnly today)
    {
        if (status.Id != StatusId)
        {
            throw new DomainValidationException($"Status {status.Id} isn't work item {Id}'s status.");
        }

        return FollowStatusCategory(status, today);
    }

    /// <summary>
    /// The completion rule: an item in a Done status keeps the date it was first done on, or
    /// takes <paramref name="today"/>; an item in any other status has none.
    /// </summary>
    private IReadOnlyList<FieldChange> FollowStatusCategory(Status status, DateOnly today)
    {
        var completionDate = status.IsDone ? CompletionDate ?? today : (DateOnly?)null;
        var changed = new ChangedFields();
        CompletionDate = changed.Set(WorkItemField.CompletionDate, CompletionDate, completionDate);
        return changed.Changes;
    }

    /// <summary>
    /// Moves the item under another parent (or to the top level of its Space when
    /// <paramref name="parent"/> is null), keeping its status and Space. The caller must
    /// already have checked the new parent isn't one of this item's descendants; an item being
    /// its own parent, or a parent in another Space, is rejected here.
    /// </summary>
    public IReadOnlyList<FieldChange> MoveToParent(WorkItem? parent, double rank)
    {
        EnsureCanMoveUnder(parent);

        var changed = new ChangedFields();
        ParentId = changed.Set(WorkItemField.ParentId, ParentId, parent?.Id);
        Rank = changed.Set(WorkItemField.Rank, Rank, rank);
        return changed.Changes;
    }

    /// <summary>
    /// The checks <see cref="MoveToParent"/> makes, for a caller that wants to reject a move
    /// before working out where in the new cell the item goes.
    /// </summary>
    public void EnsureCanMoveUnder(WorkItem? parent)
    {
        if (parent?.Id == Id)
        {
            throw new CyclicParentException(Id, Id);
        }

        if (parent is not null && parent.SpaceId != SpaceId)
        {
            throw new CrossSpaceParentException(Id, parent.Id);
        }
    }

    /// <summary>
    /// Moves the item into another Space, under <paramref name="parent"/> there or at its top
    /// level, taking that Space's <paramref name="values"/>. This is the explicit operation
    /// reparenting refuses to be (ADR-0001). Its descendants then follow through
    /// <see cref="FollowParentToSpace"/>. The caller must have checked the values are the
    /// target Space's.
    /// </summary>
    public IReadOnlyList<FieldChange> MoveToSpace(
        Guid spaceId,
        WorkItem? parent,
        double rank,
        SpaceMoveValues values,
        DateOnly today)
    {
        EnsureCanMoveToSpace(spaceId, parent);

        var changed = new ChangedFields();
        SpaceId = changed.Set(WorkItemField.SpaceId, SpaceId, spaceId);
        ParentId = changed.Set(WorkItemField.ParentId, ParentId, parent?.Id);
        Rank = changed.Set(WorkItemField.Rank, Rank, rank);
        return [.. changed.Changes, .. TakeSpaceValues(values, today)];
    }

    /// <summary>
    /// The checks <see cref="MoveToSpace"/> makes, for a caller that wants to reject a move
    /// before loading the subtree and working out its mapping.
    /// </summary>
    public void EnsureCanMoveToSpace(Guid spaceId, WorkItem? parent)
    {
        if (spaceId == SpaceId)
        {
            throw new DomainValidationException(
                $"Work item {Id} is already in Space {spaceId}; reparent it to move it within its Space.");
        }

        if (parent is not null && parent.SpaceId != spaceId)
        {
            throw new CrossSpaceParentException(Id, parent.Id);
        }
    }

    /// <summary>
    /// Moves a descendant of a moved item into the Space its <paramref name="parent"/> has just
    /// moved to, keeping its place under that parent, so children still share their parent's
    /// Space.
    /// </summary>
    public IReadOnlyList<FieldChange> FollowParentToSpace(WorkItem parent, SpaceMoveValues values, DateOnly today)
    {
        if (parent.Id != ParentId)
        {
            throw new DomainValidationException($"Work item {parent.Id} isn't the parent of work item {Id}.");
        }

        if (parent.SpaceId == SpaceId)
        {
            throw new DomainValidationException($"Work item {parent.Id} hasn't moved out of work item {Id}'s Space.");
        }

        var changed = new ChangedFields();
        SpaceId = changed.Set(WorkItemField.SpaceId, SpaceId, parent.SpaceId);
        return [.. changed.Changes, .. TakeSpaceValues(values, today)];
    }

    /// <summary>
    /// The Type goes through <see cref="ChangeType"/>, so the custom field rule is the same one,
    /// and the new status's category settles the completion date as any status change does.
    /// </summary>
    private IReadOnlyList<FieldChange> TakeSpaceValues(SpaceMoveValues values, DateOnly today)
    {
        var changed = new ChangedFields();
        StatusId = changed.Set(WorkItemField.StatusId, StatusId, values.Status.Id);
        PriorityId = changed.Set(WorkItemField.PriorityId, PriorityId, values.PriorityId);
        return [.. changed.Changes, .. FollowStatusCategory(values.Status, today), .. ChangeType(values.TypeId)];
    }

    /// <summary>Changes the item's place within its current board cell.</summary>
    public void Reposition(double rank)
    {
        Rank = rank;
    }

    /// <summary>
    /// Changes the start and/or end date; a date left out is kept, and the start-before-end
    /// rule is checked against the dates the item ends up with. Returns the fields whose
    /// value changed.
    /// </summary>
    public IReadOnlyList<FieldChange> Reschedule(Optional<DateOnly?> startDate, Optional<DateOnly?> endDate)
    {
        var newStart = startDate.GetValueOr(StartDate);
        var newEnd = endDate.GetValueOr(EndDate);
        if (newStart is not null && newEnd is not null && newStart > newEnd)
        {
            throw new InvalidWorkItemScheduleException(Id);
        }

        var changed = new ChangedFields();
        StartDate = changed.Set(WorkItemField.StartDate, StartDate, newStart);
        EndDate = changed.Set(WorkItemField.EndDate, EndDate, newEnd);
        return changed.Changes;
    }

    public IReadOnlyList<FieldChange> AssignTo(Guid? userId)
    {
        var changed = new ChangedFields();
        AssignedToUserId = changed.Set(WorkItemField.AssignedToUserId, AssignedToUserId, userId);
        return changed.Changes;
    }

    /// <summary>
    /// A copy of this item for one occurrence of a repetition: same descriptive fields,
    /// assignee, tags and Space, placed under <paramref name="parentId"/> and scheduled on
    /// <paramref name="date"/>. Comments, links, schedule, repetition and completion date are
    /// not copied: the copy is complete only when <paramref name="status"/> is a Done one, as of
    /// <paramref name="today"/>. The caller places it under the template's parent or a copy of
    /// one of its descendants, so the copy's Space matches its parent's.
    /// </summary>
    public WorkItem CopyForOccurrence(Guid? parentId, Status status, double rank, DateOnly date, DateOnly today)
    {
        var copy = New(SpaceId, parentId, Title, Description, status, rank, today, TypeId, PriorityId);
        copy.AssignedToUserId = AssignedToUserId;
        copy._tags = [.. _tags];
        copy._customFields = new Dictionary<Guid, JsonElement>(_customFields);
        copy.When = When;
        copy.StartDate = date;
        copy.EndDate = date;
        return copy;
    }

    /// <summary>
    /// Marks this item as the occurrence of <paramref name="sourceId"/> for
    /// <paramref name="date"/>; the pair is unique, so an occurrence can't be generated twice.
    /// </summary>
    public void MarkAsOccurrenceOf(Guid sourceId, DateOnly date)
    {
        RecurrenceSourceId = sourceId;
        RecurrenceDate = date;
    }

    /// <summary>
    /// Moves the item to the Trash as part of the delete made on <paramref name="trashRootId"/>
    /// (this item, or the ancestor whose subtree is going).
    /// </summary>
    public void MoveToTrash(Guid trashRootId, DateTimeOffset trashedAtUtc)
    {
        if (IsTrashed)
        {
            throw new DomainValidationException($"Work item {Id} is already in the Trash.");
        }

        TrashedAtUtc = trashedAtUtc;
        TrashRootId = trashRootId;
    }

    /// <summary>
    /// Takes the item out of the Trash where it is. Placing a restored item under a parent that
    /// still exists is the caller's job, since that needs other rows.
    /// </summary>
    public void RestoreFromTrash()
    {
        if (!IsTrashed)
        {
            throw new DomainValidationException($"Work item {Id} isn't in the Trash.");
        }

        TrashedAtUtc = null;
        TrashRootId = null;
    }

    /// <summary>
    /// Replaces the full tag list. Tags are trimmed and de-duplicated case-insensitively,
    /// keeping the first spelling; a blank tag is rejected. Returns the fields whose value
    /// changed.
    /// </summary>
    public IReadOnlyList<FieldChange> SetTags(IEnumerable<string> tags)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            var trimmed = tag.Trim();
            if (trimmed.Length == 0)
            {
                throw new InvalidWorkItemTagException(Id);
            }

            TextValidation.RequireMaxLength(trimmed, "Each tag", TagMaxLength);

            if (seen.Add(trimmed))
            {
                normalized.Add(trimmed);
            }
        }

        if (normalized.Count > MaxTags)
        {
            throw new DomainValidationException($"A work item can have at most {MaxTags} tags.");
        }

        if (normalized.SequenceEqual(_tags, StringComparer.Ordinal))
        {
            return [];
        }

        var previous = _tags;
        _tags = normalized;
        return [new FieldChange(WorkItemField.Tags, previous, normalized)];
    }
}
