namespace Weaver.Application.Services;

/// <summary>
/// "What changed since T": the work items with history entries since a moment, each with those
/// entries, deletes included (ADR-0005). Read from the history, so REST and MCP agree.
/// </summary>
public interface IWorkItemChangesService
{
    /// <summary>
    /// One page of changed items, in the order each first changed since <see cref="WorkItemChangesQuery.Since"/>.
    /// Throws <see cref="Weaver.Domain.Exceptions.DomainValidationException"/> for a bad limit or cursor.
    /// </summary>
    Task<WorkItemChangesPage> ListSinceAsync(WorkItemChangesQuery query, CancellationToken ct = default);
}
