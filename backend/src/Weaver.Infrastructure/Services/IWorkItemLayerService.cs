using Weaver.Domain;

namespace Weaver.Infrastructure.Services;

public interface IWorkItemLayerService
{
    Task<IReadOnlyList<WorkItemLayer>> GetAllAsync(CancellationToken ct = default);
}
