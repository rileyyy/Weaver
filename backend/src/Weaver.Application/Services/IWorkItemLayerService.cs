using Weaver.Domain;

namespace Weaver.Application.Services;

public interface IWorkItemLayerService : ILookupService<WorkItemLayer>
{
    Task<WorkItemLayer> CreateAsync(Guid actorUserId, string name, string color, CancellationToken ct = default);

    Task<WorkItemLayer> UpdateAsync(Guid actorUserId, Guid id, string name, string color, CancellationToken ct = default);
}
