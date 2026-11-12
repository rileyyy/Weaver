using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>
/// One change a run made and what undoing the run did with it. <see cref="Number"/> is null for an
/// item that has since been purged.
/// </summary>
public record RunUndoChangeDto(Guid WorkItemId, int? Number, string Title, string Field, RunUndoOutcome Outcome)
{
    public static RunUndoChangeDto FromChange(RunUndoChange change) =>
        new(change.WorkItemId, change.Number, change.Title, change.Field, change.Outcome);
}
