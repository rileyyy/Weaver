namespace Weaver.Api.Contracts;

public record ReparentWorkItemRequest(Guid? ParentId, Guid? AfterId);
