namespace Weaver.Api.Contracts;

/// <summary>Every value's id, in its new display order.</summary>
public record ReorderLookupRequest(IReadOnlyList<Guid> OrderedIds);
