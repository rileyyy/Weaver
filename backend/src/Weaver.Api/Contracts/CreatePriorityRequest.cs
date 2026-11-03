namespace Weaver.Api.Contracts;

/// <summary>The body for adding a priority, as the most urgent, to a Space's.</summary>
public record CreatePriorityRequest(Guid SpaceId, string Name);
