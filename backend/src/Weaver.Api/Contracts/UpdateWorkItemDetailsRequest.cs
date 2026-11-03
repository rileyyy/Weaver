using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>A field left out of the JSON is unchanged; <c>null</c> clears it.</summary>
public record UpdateWorkItemDetailsRequest(
    Optional<string> Title,
    Optional<string?> Description,
    Optional<Guid?> TypeId,
    Optional<Guid?> PriorityId,
    Optional<string?> When,
    uint? ExpectedVersion = null);
