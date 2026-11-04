namespace Weaver.Domain;

/// <summary>
/// One choice of a single- or multi-select field. Values store its <see cref="Id"/>, so
/// renaming an option doesn't change what items have chosen.
/// </summary>
public sealed record CustomFieldOption(Guid Id, string Name);
