using System.Text.Json;

namespace Weaver.Runner.WeaverApi;

/// <summary>
/// The parts of Weaver's <c>WorkItemHistoryEntryDto</c> the runner reads: which field changed,
/// when, in which run, and its new value as JSON (null when the change removed it).
/// </summary>
public sealed record WeaverHistoryEntry(DateTimeOffset ChangedAtUtc, string? RunId, string Field, JsonElement? NewValue);
