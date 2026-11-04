namespace Weaver.Domain;

/// <summary>
/// One option in a select field's new option list: an existing option (by <see cref="Id"/>),
/// possibly renamed, or a new one when <see cref="Id"/> is null.
/// </summary>
public sealed record CustomFieldOptionRevision(Guid? Id, string Name);
