namespace Weaver.Domain;

/// <summary>
/// A refresh token's server-side record. The token value itself is never
/// stored — only <see cref="TokenHash"/>, a SHA-256 hash of it — so a
/// database read can't leak a usable credential. Rotated on every use
/// (see <see cref="ReplacedByTokenHash"/>): issuing a new refresh token
/// always revokes the one it was exchanged for, and presenting a rotated
/// token again revokes every token issued from it.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public uint Version { get; set; }

    public User? User { get; set; }

    public bool IsActive => RevokedAtUtc is null && DateTimeOffset.UtcNow < ExpiresAtUtc;
}
