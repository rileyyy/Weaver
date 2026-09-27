using Weaver.Domain;

namespace Weaver.Application.Services;

public interface IUserService
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<User>> ListAsync(CancellationToken ct = default);
}
