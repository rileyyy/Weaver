using Weaver.Domain;

namespace Weaver.Application.Services;

public interface IUserService
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Active users only: this feeds assignee pickers. Admins see everyone through <see cref="IUserAdministrationService"/>.</summary>
    Task<IReadOnlyList<User>> ListAsync(CancellationToken ct = default);
}
