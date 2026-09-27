using Weaver.Domain;

namespace Weaver.Application.Services;

public interface IWorkItemLayerService
{
    Task<IReadOnlyList<WorkItemLayer>> GetAllAsync(CancellationToken ct = default);
}
