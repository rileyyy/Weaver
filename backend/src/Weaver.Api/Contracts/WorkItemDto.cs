using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record WorkItemDto(
    Guid Id,
    int Number,
    Guid? ParentId,
    string Title,
    string? Description,
    Guid StatusId,
    Guid? LayerId,
    WorkItemPriority Priority,
    Guid? AssignedToUserId,
    IReadOnlyList<string> Tags,
    double Rank,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    uint Version)
{
    public static WorkItemDto FromEntity(WorkItem item) => new(
        item.Id,
        item.Number,
        item.ParentId,
        item.Title,
        item.Description,
        item.StatusId,
        item.LayerId,
        item.Priority,
        item.AssignedToUserId,
        item.Tags,
        item.Rank,
        item.StartDate,
        item.EndDate,
        item.CreatedAtUtc,
        item.UpdatedAtUtc,
        item.Version);
}

public record WorkItemLayerDto(Guid Id, string Name, int Order)
{
    public static WorkItemLayerDto FromEntity(WorkItemLayer layer) => new(layer.Id, layer.Name, layer.Order);
}

public record CreateWorkItemRequest(
    string Title,
    string? Description,
    Guid? ParentId,
    Guid StatusId,
    Guid? AfterId,
    Guid? LayerId = null,
    WorkItemPriority Priority = WorkItemPriority.Medium);

public record ChangeWorkItemStatusRequest(Guid StatusId, Guid? AfterId);

public record ReparentWorkItemRequest(Guid? ParentId, Guid? AfterId);

public record RescheduleWorkItemRequest(DateTimeOffset? StartDate, DateTimeOffset? EndDate, uint? ExpectedVersion = null);

public record UpdateWorkItemDetailsRequest(
    string Title,
    string? Description,
    Guid? LayerId,
    WorkItemPriority Priority,
    uint? ExpectedVersion = null);

public record AssignWorkItemRequest(Guid? UserId);

public record SetTagsWorkItemRequest(IReadOnlyList<string> Tags, uint? ExpectedVersion = null);
