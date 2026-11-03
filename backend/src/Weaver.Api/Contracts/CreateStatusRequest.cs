using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>The body for adding a status to the end of a Space's columns.</summary>
public record CreateStatusRequest(Guid SpaceId, string Name, StatusCategory Category, string Color);
