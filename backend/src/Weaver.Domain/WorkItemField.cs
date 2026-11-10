namespace Weaver.Domain;

/// <summary>
/// The names writes report as changed. They match the work item's JSON property names, so a
/// client can tell which of the fields it holds are now stale.
/// </summary>
public static class WorkItemField
{
    public const string Title = "title";
    public const string Description = "description";
    public const string TypeId = "typeId";
    public const string PriorityId = "priorityId";
    public const string When = "when";
    public const string StatusId = "statusId";
    public const string ParentId = "parentId";
    public const string SpaceId = "spaceId";
    public const string Rank = "rank";
    public const string StartDate = "startDate";
    public const string EndDate = "endDate";
    public const string CompletionDate = "completionDate";
    public const string AssignedToUserId = "assignedToUserId";
    public const string Tags = "tags";

    /// <summary>
    /// Built-in names have no dot, so a custom field's name (<c>fields.&lt;id&gt;</c>, the path
    /// of its value in the item's JSON) can't collide with one, now or later.
    /// </summary>
    public const string CustomFieldPrefix = "fields.";

    public static string CustomField(Guid fieldId) => CustomFieldPrefix + fieldId;

    /// <summary>The field id in a <see cref="CustomField"/> name; null for every other name.</summary>
    public static Guid? CustomFieldId(string name) =>
        name.StartsWith(CustomFieldPrefix, StringComparison.Ordinal)
            && Guid.TryParse(name.AsSpan(CustomFieldPrefix.Length), out var id)
            ? id
            : null;
}
