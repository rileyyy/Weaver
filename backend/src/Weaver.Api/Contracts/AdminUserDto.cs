using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// A user as the admin page sees it. <see cref="LockedUntilUtc"/> is only set while a lockout
/// is still in force, so clients don't have to compare it with their own clock.
/// </summary>
public record AdminUserDto(
    Guid Id,
    string Username,
    UserKind Kind,
    bool IsAdmin,
    bool IsActive,
    DateTimeOffset? LockedUntilUtc,
    DateTimeOffset CreatedAtUtc)
{
    public static AdminUserDto FromEntity(User user, DateTimeOffset now) => new(
        user.Id,
        user.Username,
        user.Kind,
        user.IsAdmin,
        user.IsActive,
        user.IsLockedOutAt(now) ? user.LockedUntilUtc : null,
        user.CreatedAtUtc);
}
