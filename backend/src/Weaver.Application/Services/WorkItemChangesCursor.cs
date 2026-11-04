namespace Weaver.Application.Services;

/// <summary>
/// Where a page of "changes since T" ends: the id of the first history entry (since T) of the
/// last item on the page. Entry ids only grow and an item's first entry since T never changes,
/// so the next page starts exactly after it, however many changes land in between.
/// </summary>
/// <remarks>Sent to clients as an <see cref="OpaqueCursor"/>.</remarks>
public readonly record struct WorkItemChangesCursor(long AfterEntryId)
{
    private const string Prefix = "v1:";

    public static WorkItemChangesCursor Start { get; } = new(0);

    public string Encode() => OpaqueCursor.Encode(Prefix, AfterEntryId);

    /// <summary>No token is the first page.</summary>
    public static WorkItemChangesCursor Parse(string? token) => new(OpaqueCursor.Parse(Prefix, token, Start.AfterEntryId));
}
