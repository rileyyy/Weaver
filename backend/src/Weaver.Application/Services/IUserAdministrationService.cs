using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// Account management for administrators. Every operation takes the acting user's id and
/// refuses anyone who isn't an active admin.
/// </summary>
public interface IUserAdministrationService
{
    /// <summary>Every account, including deactivated ones.</summary>
    Task<IReadOnlyList<User>> ListAsync(Guid actorUserId, CancellationToken ct = default);

    Task<User> CreateAsync(Guid actorUserId, string username, string password, bool isAdmin, CancellationToken ct = default);

    Task<User> RenameAsync(Guid actorUserId, Guid userId, string username, CancellationToken ct = default);

    /// <summary>Also clears any lockout and signs the user out everywhere.</summary>
    Task<User> ResetPasswordAsync(Guid actorUserId, Guid userId, string password, CancellationToken ct = default);

    Task<User> UnlockAsync(Guid actorUserId, Guid userId, CancellationToken ct = default);

    Task<User> SetAdminAsync(Guid actorUserId, Guid userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>
    /// Deactivating signs the user out everywhere and unassigns their work items, since a
    /// deactivated user can't be picked as an assignee.
    /// </summary>
    Task<User> SetActiveAsync(Guid actorUserId, Guid userId, bool isActive, CancellationToken ct = default);

    /// <summary>Refused for a user who has written comments; deactivate them instead.</summary>
    Task DeleteAsync(Guid actorUserId, Guid userId, CancellationToken ct = default);
}
