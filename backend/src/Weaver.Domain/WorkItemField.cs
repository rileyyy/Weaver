namespace Weaver.Domain;

/// <summary>
/// The names writes report as changed. They match the work item's JSON property names, so a
/// client can tell which of the fields it holds are now stale.
/// </summary>
public static class WorkItemField
{
    public const string Title = "title";
    public const string Description = "description";
    public const string LayerId = "layerId";
    public const string PriorityId = "priorityId";
    public const string StatusId = "statusId";
    public const string ParentId = "parentId";
    public const string Rank = "rank";
    public const string StartDate = "startDate";
    public const string EndDate = "endDate";
    public const string AssignedToUserId = "assignedToUserId";
    public const string Tags = "tags";
}
