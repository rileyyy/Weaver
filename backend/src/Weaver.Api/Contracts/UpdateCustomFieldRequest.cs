namespace Weaver.Api.Contracts;

/// <summary>
/// The body for editing a field; its Type and kind never change. <see cref="Options"/> is the
/// whole new option list in order: an option left out is removed, and items lose that choice.
/// </summary>
public record UpdateCustomFieldRequest(string Name, IReadOnlyList<CustomFieldOptionRequest>? Options = null);
