using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

/// <summary>
/// A long-lived credential an agent account signs in with (ADR-0004). Only
/// <see cref="KeyHash"/> is stored, so a database read can't leak a usable key; the key itself
/// is shown once, when it's issued. Revoking keeps the row, so what the key did stays
/// attributable to it.
/// </summary>
public class ApiKey : IHasCreatedAt
{
    public const int MaxLabelLength = 100;

    /// <summary>How stale <see cref="LastUsedAtUtc"/> may get before a use is written again.</summary>
    public static readonly TimeSpan LastUsedPrecision = TimeSpan.FromMinutes(1);

    /// <summary>For EF Core.</summary>
    private ApiKey()
    {
        Label = string.Empty;
        KeyHash = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Label { get; private set; }

    public ApiKeyScope Scope { get; private set; }

    public string KeyHash { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? LastUsedAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public bool IsRevoked => RevokedAtUtc is not null;

    public User? User { get; private set; }

    public static ApiKey Issue(User account, string label, ApiKeyScope scope, string keyHash)
    {
        if (account.Kind != UserKind.Agent)
        {
            throw new NotAnAgentAccountException(account.Username);
        }

        var trimmedLabel = label?.Trim() ?? string.Empty;
        TextValidation.RequireText(trimmedLabel, "Label", MaxLabelLength);

        return new ApiKey
        {
            Id = Guid.NewGuid(),
            UserId = account.Id,
            Label = trimmedLabel,
            Scope = scope,
            KeyHash = keyHash,
        };
    }

    /// <summary>Revokes the key; a no-op if it's already revoked.</summary>
    public void Revoke(DateTimeOffset now)
    {
        RevokedAtUtc ??= now;
    }

    /// <summary>
    /// Notes that the key was just used. Returns whether <see cref="LastUsedAtUtc"/> changed,
    /// i.e. whether there's anything to save.
    /// </summary>
    public bool RecordUse(DateTimeOffset now)
    {
        if (LastUsedAtUtc is { } lastUsed && now - lastUsed < LastUsedPrecision)
        {
            return false;
        }

        LastUsedAtUtc = now;
        return true;
    }
}
