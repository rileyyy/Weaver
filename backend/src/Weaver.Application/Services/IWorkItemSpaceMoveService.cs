namespace Weaver.Application.Services;

/// <summary>
/// Moves a work item, with its whole subtree, into another Space: the explicit operation that
/// reparenting refuses to be (ADR-0001). Each item takes the target Space's configuration:
/// <list type="bullet">
/// <item>a status maps as the caller says, else to the target status with the same name
/// (ignoring case), else to the target Space's first status in the same category, else to its
/// first status;</item>
/// <item>a Type or priority maps as the caller says, else to the one with the same name, else
/// it's cleared;</item>
/// <item>custom field values are cleared, as with any change of Type (ticket 14), since the
/// target Space's Types have other fields.</item>
/// </list>
/// Links, comments, attachments, tags, dates and the assignee stay as they are. Sub-items in the
/// Trash move too, so restoring one never puts it in a different Space from its parent.
/// </summary>
public interface IWorkItemSpaceMoveService
{
    /// <summary>
    /// What <see cref="MoveAsync"/> would do with no maps, for showing the mapping before the
    /// move is confirmed. Changes nothing.
    /// </summary>
    Task<SpaceMovePlan> PlanAsync(Guid id, Guid targetSpaceId, CancellationToken ct = default);

    /// <summary>
    /// Moves the item under <paramref name="targetParentId"/> in <paramref name="targetSpaceId"/>,
    /// or to its top level, first in its new cell, with its descendants in one save, recording
    /// every moved item's changes in its history. Map keys must be values of the item's Space and
    /// map values the target Space's. Returns the moved item and its changed fields.
    /// </summary>
    Task<WorkItemWriteResult> MoveAsync(
        Guid id,
        Guid targetSpaceId,
        Guid? targetParentId,
        SpaceMoveMaps maps,
        uint? expectedVersion = null,
        CancellationToken ct = default);
}
