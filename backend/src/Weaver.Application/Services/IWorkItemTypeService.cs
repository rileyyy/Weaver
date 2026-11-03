using Weaver.Domain;

namespace Weaver.Application.Services;

public interface IWorkItemTypeService : IGlobalLookupService<WorkItemType>
{
    Task<WorkItemType> CreateAsync(Guid actorUserId, string name, string color, CancellationToken ct = default);

    Task<WorkItemType> UpdateAsync(Guid actorUserId, Guid id, string name, string color, CancellationToken ct = default);
}
