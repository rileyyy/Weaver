namespace Weaver.Api.Contracts;

/// <summary>
/// <see cref="SpaceId"/> is required for a top-level item. A child is created in its parent's
/// Space, so it can be left out (or must match).
/// </summary>
public record CreateWorkItemRequest(
    string Title,
    string? Description,
    Guid? ParentId,
    Guid StatusId,
    Guid? AfterId,
    Guid? LayerId = null,
    Guid? PriorityId = null,
    Guid? SpaceId = null);
