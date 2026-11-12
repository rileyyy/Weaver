namespace Weaver.Application.Services;

/// <summary>
/// Undoing an agent run (ticket 54): what the run added is removed and the fields it changed get
/// their old values back, taken from history. A field someone else changed after the run keeps
/// their change; the rest of the item is still undone. Comments are kept, since they're part of
/// the item's public record. The undo's own changes are recorded in history as the admin's, all
/// or nothing. Refused unless the caller is an active admin.
/// </summary>
public interface IRunUndoService
{
    /// <summary>
    /// Throws <see cref="Weaver.Domain.Exceptions.RunNotFoundException"/> when no entry belongs
    /// to the run. Undoing a run twice changes nothing the second time: everything the first undo
    /// put back is a later change than the run's.
    /// </summary>
    Task<RunUndoResult> UndoAsync(RunKey run, CancellationToken ct = default);
}
