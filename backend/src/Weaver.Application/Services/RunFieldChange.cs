namespace Weaver.Application.Services;

/// <summary>
/// A field one run changed: the value it had before the run (as stored JSON) and the run's first
/// entry for it. Any change someone else made after that entry, even between two of the run's
/// own, is theirs to keep.
/// </summary>
internal sealed record RunFieldChange(string Field, string? OldValueJson, long FirstEntryId);
