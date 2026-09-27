namespace Weaver.Api.Contracts;

public record RescheduleWorkItemRequest(DateOnly? StartDate, DateOnly? EndDate, uint? ExpectedVersion = null);
