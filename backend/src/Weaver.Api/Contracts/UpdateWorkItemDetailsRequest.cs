using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record UpdateWorkItemDetailsRequest(
    string Title,
    string? Description,
    Guid? LayerId,
    WorkItemPriority Priority,
    uint? ExpectedVersion = null);
