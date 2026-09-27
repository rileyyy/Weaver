namespace Weaver.Domain;

/// <summary>Also stamped with the time of every save that changes the entity.</summary>
public interface IHasUpdatedAt : IHasCreatedAt
{
    DateTimeOffset UpdatedAtUtc { get; }
}
