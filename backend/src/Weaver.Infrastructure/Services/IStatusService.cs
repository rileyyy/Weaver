using Weaver.Domain;

namespace Weaver.Infrastructure.Services;

public interface IStatusService
{
    Task<IReadOnlyList<Status>> GetAllAsync(CancellationToken ct = default);
}
