using Weaver.Domain;

namespace Weaver.Application.Services;

public interface IWorkItemLayerService : ILookupService<WorkItemLayer>
{
    Task<WorkItemLayer> CreateAsync(Guid actorUserId, string name, CancellationToken ct = default);

    Task<WorkItemLayer> RenameAsync(Guid actorUserId, Guid id, string name, CancellationToken ct = default);
}
