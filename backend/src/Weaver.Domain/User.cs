namespace Weaver.Domain;

public enum UserKind
{
    Human,
    Agent,
}

public class User
{
    public Guid Id { get; set; }

    public required string Username { get; set; }

    /// <summary>
    /// Lowercased <see cref="Username"/>, used for case-insensitive lookup and
    /// uniqueness — Postgres has no built-in case-insensitive unique index.
    /// </summary>
    public required string NormalizedUsername { get; set; }

    public required string PasswordHash { get; set; }

    public UserKind Kind { get; set; } = UserKind.Human;

    public int FailedLoginAttempts { get; set; }

    public DateTimeOffset? LockedUntilUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
