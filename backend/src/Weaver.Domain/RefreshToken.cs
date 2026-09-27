namespace Weaver.Domain;

/// <summary>
/// A refresh token's server-side record. The token value itself is never
/// stored — only <see cref="TokenHash"/>, a SHA-256 hash of it — so a
/// database read can't leak a usable credential. Rotated on every use
/// (see <see cref="ReplacedByTokenHash"/>): issuing a new refresh token
/// always revokes the one it was exchanged for, and presenting a rotated
/// token again revokes every token issued from it.
/// </summary>
public class RefreshToken : IHasCreatedAt
{
    /// <summary>For EF Core.</summary>
    private RefreshToken()
    {
        TokenHash = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public uint Version { get; private set; }

    public User? User { get; private set; }

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTimeOffset expiresAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = tokenHash,
        ExpiresAtUtc = expiresAtUtc,
    };

    public bool IsActiveAt(DateTimeOffset now) => RevokedAtUtc is null && now < ExpiresAtUtc;

    /// <summary>
    /// True if this token was rotated more than <paramref name="gracePeriod"/> ago, i.e. it is
    /// being replayed rather than raced by a second tab.
    /// </summary>
    public bool IsReplayedAt(DateTimeOffset now, TimeSpan gracePeriod) =>
        ReplacedByTokenHash is not null && RevokedAtUtc is { } revokedAt && now - revokedAt > gracePeriod;

    /// <summary>Revokes the token; a no-op if it's already revoked.</summary>
    public void Revoke(DateTimeOffset now)
    {
        RevokedAtUtc ??= now;
    }

    public void RotateTo(string replacementTokenHash, DateTimeOffset now)
    {
        RevokedAtUtc = now;
        ReplacedByTokenHash = replacementTokenHash;
    }
}
