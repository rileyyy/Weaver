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
    DateOnly? StartDate,
    DateOnly? EndDate,
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
