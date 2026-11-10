namespace Weaver.Api.Contracts;

/// <summary>
/// The body for editing a field; its Type and kind never change. Like any PUT it replaces the
/// whole definition: <see cref="Options"/> is the whole new option list in order (an option left
/// out is removed, and items lose that choice), and leaving <see cref="RollsUp"/> out turns the
/// roll-up off.
/// </summary>
public record UpdateCustomFieldRequest(
    string Name,
    IReadOnlyList<CustomFieldOptionRequest>? Options = null,
    bool RollsUp = false);
