using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>The body for editing a status; its Space never changes.</summary>
public record StatusDetailsRequest(string Name, StatusCategory Category, string Color);
