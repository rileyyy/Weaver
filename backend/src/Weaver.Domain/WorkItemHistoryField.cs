namespace Weaver.Domain;

/// <summary>
/// The history entries that aren't a change to one of the item's own fields (those use
/// <see cref="WorkItemField"/>). Each says what its old and new values hold.
/// </summary>
public static class WorkItemHistoryField
{
    /// <summary>The item was created. New value: its title.</summary>
    public const string Created = "created";

    /// <summary>The item was deleted (moved to the Trash). Old value: its title.</summary>
    public const string Deleted = "deleted";

    /// <summary>The item was restored from the Trash. New value: its title.</summary>
    public const string Restored = "restored";

    /// <summary>
    /// The item was purged from the Trash, for good. Old value: its title. The item's history
    /// outlives it; this is always its last entry.
    /// </summary>
    public const string Purged = "purged";

    /// <summary>A comment was added (new value only), edited (both) or deleted (old value only). Values: the body.</summary>
    public const string Comment = "comment";

    /// <summary>
    /// A link was added (new value) or removed (old value), recorded on both items. Values:
    /// <c>{ "workItemId", "type" }</c>, the other item and the link's type seen from this item.
    /// Entries from before links had types hold only the other item's id.
    /// </summary>
    public const string Link = "link";

    /// <summary>A URL link was added (new value) or removed (old value). Values: <c>{ "url", "label" }</c>.</summary>
    public const string UrlLink = "urlLink";

    /// <summary>A file was attached (new value) or removed (old value). Values: <c>{ "id", "fileName" }</c>.</summary>
    public const string Attachment = "attachment";

    /// <summary>The item started (new value), changed or stopped (old value) repeating. Values: the schedule.</summary>
    public const string Recurrence = "recurrence";
}
