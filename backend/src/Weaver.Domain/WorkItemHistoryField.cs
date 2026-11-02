namespace Weaver.Domain;

/// <summary>
/// The history entries that aren't a change to one of the item's own fields (those use
/// <see cref="WorkItemField"/>). Each says what its old and new values hold.
/// </summary>
public static class WorkItemHistoryField
{
    /// <summary>The item was created. New value: its title.</summary>
    public const string Created = "created";

    /// <summary>The item was deleted. Old value: its title.</summary>
    public const string Deleted = "deleted";

    /// <summary>A comment was added (new value only), edited (both) or deleted (old value only). Values: the body.</summary>
    public const string Comment = "comment";

    /// <summary>A link was added (new value) or removed (old value). Values: the other item's id.</summary>
    public const string Link = "link";

    /// <summary>The item started (new value), changed or stopped (old value) repeating. Values: the schedule.</summary>
    public const string Recurrence = "recurrence";
}
