using Weaver.Domain;

namespace Weaver.Application.Services;

public interface IStatusService
{
    Task<IReadOnlyList<Status>> GetAllAsync(CancellationToken ct = default);
}
