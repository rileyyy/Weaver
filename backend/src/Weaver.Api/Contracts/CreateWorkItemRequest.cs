using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record CreateWorkItemRequest(
    string Title,
    string? Description,
    Guid? ParentId,
    Guid StatusId,
    Guid? AfterId,
    Guid? LayerId = null,
    WorkItemPriority Priority = WorkItemPriority.Medium);
