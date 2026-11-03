namespace Weaver.Api.Contracts;

/// <summary>The body for adding a Type to the end of a Space's.</summary>
public record CreateTypeRequest(Guid SpaceId, string Name, string Color);
