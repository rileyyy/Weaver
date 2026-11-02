using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>A key as the admin page sees it: never the key itself, which only its creation returns.</summary>
public record ApiKeyDto(
    Guid Id,
    string Label,
    ApiKeyScope Scope,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastUsedAtUtc,
    DateTimeOffset? RevokedAtUtc)
{
    public static ApiKeyDto FromEntity(ApiKey key) => new(
        key.Id,
        key.Label,
        key.Scope,
        key.CreatedAtUtc,
        key.LastUsedAtUtc,
        key.RevokedAtUtc);
}
