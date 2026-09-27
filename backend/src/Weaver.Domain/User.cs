namespace Weaver.Domain;

public class User : IHasUpdatedAt
{
    public const int MaxFailedLoginAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>For EF Core.</summary>
    private User()
    {
        Username = string.Empty;
        NormalizedUsername = string.Empty;
        PasswordHash = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Username { get; private set; }

    /// <summary>
    /// Lowercased <see cref="Username"/>, used for case-insensitive lookup and
    /// uniqueness — Postgres has no built-in case-insensitive unique index.
    /// </summary>
    public string NormalizedUsername { get; private set; }

    public string PasswordHash { get; private set; }

    public UserKind Kind { get; private set; } = UserKind.Human;

    public int FailedLoginAttempts { get; private set; }

    public DateTimeOffset? LockedUntilUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// A new human user. Username rules and normalization are the auth service's concern;
    /// the password hash is set separately because hashing needs the user instance.
    /// </summary>
    public static User CreateHuman(string username, string normalizedUsername) => new()
    {
        Id = Guid.NewGuid(),
        Username = username,
        NormalizedUsername = normalizedUsername,
        Kind = UserKind.Human,
    };

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
    }

    public bool IsLockedOutAt(DateTimeOffset now) => LockedUntilUtc is { } lockedUntil && lockedUntil > now;

    /// <summary>
    /// Counts a wrong password; the <see cref="MaxFailedLoginAttempts"/>th in a row locks the
    /// account for <see cref="LockoutDuration"/>.
    /// </summary>
    public void RecordFailedLogin(DateTimeOffset now)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= MaxFailedLoginAttempts)
        {
            LockedUntilUtc = now.Add(LockoutDuration);
        }
    }

    public void RecordSuccessfulLogin()
    {
        FailedLoginAttempts = 0;
        LockedUntilUtc = null;
    }
}
