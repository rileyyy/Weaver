using Weaver.Domain;

namespace Weaver.Application.Services;

public interface IWorkItemPriorityService
{
    Task<IReadOnlyList<WorkItemPriority>> GetAllAsync(CancellationToken ct = default);
}
