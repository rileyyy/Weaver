using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// <see cref="Type"/> is required: how the item in the route relates to the target. It's
/// nullable only so a missing type is rejected instead of binding as the enum's first value.
/// </summary>
public record CreateWorkItemLinkRequest(Guid TargetWorkItemId, WorkItemLinkRelation? Type);
