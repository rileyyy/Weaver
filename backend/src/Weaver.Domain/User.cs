using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

public class User : IHasUpdatedAt
{
    public const int MaxUsernameLength = 50;
    public const int MaxDescriptionLength = 500;
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

    /// <summary>What an agent account is for, e.g. which machine it runs on. Humans have none.</summary>
    public string? Description { get; private set; }

    /// <summary>May manage lookups (statuses, priorities, types) and other users.</summary>
    public bool IsAdmin { get; private set; }

    /// <summary>
    /// Set while the account is deactivated: it can't sign in or refresh, and it is left out
    /// of assignee pickers. Deactivation instead of deletion keeps the authorship of the
    /// user's comments.
    /// </summary>
    public DateTimeOffset? DeactivatedAtUtc { get; private set; }

    public bool IsActive => DeactivatedAtUtc is null;

    public int FailedLoginAttempts { get; private set; }

    public DateTimeOffset? LockedUntilUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// A new human user. Username rules and normalization are the auth service's concern;
    /// the password hash is set separately because hashing needs the user instance.
    /// </summary>
    public static User CreateHuman(string username, string normalizedUsername, bool isAdmin = false) => new()
    {
        Id = Guid.NewGuid(),
        Username = username,
        NormalizedUsername = normalizedUsername,
        Kind = UserKind.Human,
        IsAdmin = isAdmin,
    };

    /// <summary>
    /// A new agent account (ADR-0004). It has no password, so it can only sign in with an API
    /// key. Its name is shown wherever a username would be, but unlike a username it is free
    /// text, because agent runtimes are named like "Claude Code (desktop)".
    /// </summary>
    public static User CreateAgent(string name, string? description)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        TextValidation.RequireText(trimmedName, "Name", MaxUsernameLength);
        if (trimmedName.Any(char.IsControl))
        {
            throw new DomainValidationException("Name must be a single line of text.");
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is not null)
        {
            TextValidation.RequireMaxLength(trimmedDescription, "Description", MaxDescriptionLength);
        }

        return new User
        {
            Id = Guid.NewGuid(),
            Username = trimmedName,
            NormalizedUsername = trimmedName.ToLowerInvariant(),
            Kind = UserKind.Agent,
            Description = trimmedDescription,
        };
    }

    /// <summary>
    /// Username rules and normalization are <see cref="UserCredentials"/>' concern, as for
    /// <see cref="CreateHuman"/>; uniqueness needs the other rows, so it's the service's.
    /// </summary>
    public void Rename(string username, string normalizedUsername)
    {
        Username = username;
        NormalizedUsername = normalizedUsername;
    }

    /// <summary>
    /// Only humans can be administrators: an agent's key would otherwise pass every admin check,
    /// and agents must never change admin data (ADR-0004).
    /// </summary>
    public void GrantAdmin()
    {
        if (Kind == UserKind.Agent)
        {
            throw new NotAHumanAccountException(Username, "Agent accounts can't be administrators.");
        }

        IsAdmin = true;
    }

    public void RevokeAdmin()
    {
        IsAdmin = false;
    }

    public void Deactivate(DateTimeOffset now)
    {
        DeactivatedAtUtc ??= now;
    }

    public void Reactivate()
    {
        DeactivatedAtUtc = null;
    }

    /// <summary>
    /// Agents sign in with API keys only, so a password on one would be stored and never used.
    /// </summary>
    public void SetPasswordHash(string passwordHash)
    {
        if (Kind == UserKind.Agent)
        {
            throw new NotAHumanAccountException(Username, "Agent accounts sign in with API keys, not a password.");
        }

        PasswordHash = passwordHash;
    }

    public bool IsLockedOutAt(DateTimeOffset now) => LockedUntilUtc is { } lockedUntil && lockedUntil > now;

    /// <summary>
    /// Counts a wrong password; the <see cref="MaxFailedLoginAttempts"/>th in a row locks the
    /// account for <see cref="LockoutDuration"/>. An expired lockout starts a fresh count, so
    /// the first mistake after it doesn't immediately lock the account again.
    /// </summary>
    public void RecordFailedLogin(DateTimeOffset now)
    {
        if (LockedUntilUtc is { } lockedUntil && lockedUntil <= now)
        {
            FailedLoginAttempts = 0;
            LockedUntilUtc = null;
        }

        FailedLoginAttempts++;
        if (FailedLoginAttempts >= MaxFailedLoginAttempts)
        {
            LockedUntilUtc = now.Add(LockoutDuration);
        }
    }

    public void RecordSuccessfulLogin() => ClearLockout();

    public void ClearLockout()
    {
        FailedLoginAttempts = 0;
        LockedUntilUtc = null;
    }
}
