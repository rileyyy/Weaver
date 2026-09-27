namespace Weaver.Api.Contracts;

public record UpdateWorkItemDetailsRequest(
    string Title,
    string? Description,
    Guid? LayerId,
    Guid? PriorityId,
    uint? ExpectedVersion = null);
