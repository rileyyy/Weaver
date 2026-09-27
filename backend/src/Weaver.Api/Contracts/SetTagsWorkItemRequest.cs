namespace Weaver.Api.Contracts;

public record SetTagsWorkItemRequest(IReadOnlyList<string> Tags, uint? ExpectedVersion = null);
