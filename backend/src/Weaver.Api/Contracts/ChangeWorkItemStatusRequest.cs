namespace Weaver.Api.Contracts;

public record ChangeWorkItemStatusRequest(Guid StatusId, Guid? AfterId);
