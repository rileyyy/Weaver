namespace Weaver.Application.Services;

/// <summary>
/// One thing a run changed and what its undo did with it. <see cref="Field"/> is the history
/// entry's field. <see cref="Number"/> is null for an item that has since been purged.
/// </summary>
public sealed record RunUndoChange(
    Guid WorkItemId,
    int? Number,
    string Title,
    string Field,
    RunUndoOutcome Outcome);
