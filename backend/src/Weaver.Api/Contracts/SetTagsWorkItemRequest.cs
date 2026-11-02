using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// <see cref="Tags"/> replaces the whole list. Left out of the JSON, the tags are unchanged;
/// <c>null</c> clears them.
/// </summary>
public record SetTagsWorkItemRequest(Optional<IReadOnlyList<string>?> Tags, uint? ExpectedVersion = null);
