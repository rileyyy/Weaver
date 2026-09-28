using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>The body for creating a status and for editing one.</summary>
public record StatusDetailsRequest(string Name, StatusCategory Category, string Color);
