namespace Weaver.Domain;

/// <summary>
/// The View one user opens on launch. A user without one gets Overview, which is never stored,
/// so choosing Overview removes the row; deleting the View removes it too.
/// </summary>
public class DefaultView
{
    /// <summary>For EF Core.</summary>
    private DefaultView()
    {
    }

    public Guid UserId { get; private set; }

    public Guid ViewId { get; private set; }

    public static DefaultView For(Guid userId, Guid viewId) => new() { UserId = userId, ViewId = viewId };

    public void PointTo(Guid viewId) => ViewId = viewId;
}
