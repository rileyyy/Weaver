using Weaver.Domain;

namespace Weaver.Application.Services;

public interface IWorkItemPriorityService : ILookupService<WorkItemPriority>
{
    Task<WorkItemPriority> CreateAsync(Guid actorUserId, string name, CancellationToken ct = default);

    Task<WorkItemPriority> RenameAsync(Guid actorUserId, Guid id, string name, CancellationToken ct = default);
}
