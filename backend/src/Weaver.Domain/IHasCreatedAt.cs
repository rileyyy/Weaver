namespace Weaver.Domain;

/// <summary>
/// Stamped with the creation time when first saved, by the persistence layer rather than
/// by each service, so no code path can forget it.
/// </summary>
public interface IHasCreatedAt
{
    DateTimeOffset CreatedAtUtc { get; }
}
