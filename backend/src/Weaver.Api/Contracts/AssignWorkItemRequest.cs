namespace Weaver.Api.Contracts;

public record AssignWorkItemRequest(Guid? UserId, uint? ExpectedVersion = null);
