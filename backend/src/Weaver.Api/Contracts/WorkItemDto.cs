using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record WorkItemDto(
    Guid Id,
    Guid? ParentId,
    string Title,
    string? Description,
    Guid StatusId,
    double Rank,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static WorkItemDto FromEntity(WorkItem item) => new(
        item.Id,
        item.ParentId,
        item.Title,
        item.Description,
        item.StatusId,
        item.Rank,
        item.StartDate,
        item.EndDate,
        item.CreatedAtUtc,
        item.UpdatedAtUtc);
}

public record CreateWorkItemRequest(
    string Title,
    string? Description,
    Guid? ParentId,
    Guid StatusId,
    Guid? AfterId);

public record ChangeWorkItemStatusRequest(Guid StatusId, Guid? AfterId);

public record ReparentWorkItemRequest(Guid? ParentId, Guid? AfterId);

public record RescheduleWorkItemRequest(DateTimeOffset? StartDate, DateTimeOffset? EndDate);
