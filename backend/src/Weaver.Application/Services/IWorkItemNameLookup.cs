using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// The names behind the ids work items hold (Space, status, Type, priority, assignee and parent),
/// looked up for a whole batch of items at once so a response costs the same few queries however
/// many items it carries.
/// </summary>
public interface IWorkItemNameLookup
{
    Task<WorkItemNames> NamesForAsync(IReadOnlyCollection<WorkItem> items, CancellationToken ct = default);
}
