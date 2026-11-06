using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// The custom fields each Type defines (ADR-0002). Anyone can list a Type's; every change needs
/// a human admin, so agents can read definitions but never change them (ADR-0004).
/// </summary>
public interface ICustomFieldService
{
    /// <summary>The Type's fields in order; empty for an unknown Type.</summary>
    Task<IReadOnlyList<CustomFieldDefinition>> GetAllAsync(Guid typeId, CancellationToken ct = default);

    /// <summary>
    /// The fields of every Type in <paramref name="typeIds"/>, in each Type's order, for a reader
    /// that shows many Types at once.
    /// </summary>
    Task<IReadOnlyList<CustomFieldDefinition>> GetAllAsync(IReadOnlyCollection<Guid> typeIds, CancellationToken ct = default);

    /// <summary>
    /// The fields of every Type of the Space, Types in their order and each Type's fields in
    /// theirs, for a reader that shows the Space's whole tree (the Hierarchy's roll-ups). Empty
    /// for an unknown Space.
    /// </summary>
    Task<IReadOnlyList<CustomFieldDefinition>> GetAllInSpaceAsync(Guid spaceId, CancellationToken ct = default);

    /// <summary>
    /// Appends a field after the Type's last. <paramref name="optionNames"/> are a select field's
    /// options, in order; other kinds take none. The kind can't be changed later.
    /// <paramref name="rollsUp"/> can only be set on a number or currency field. Unlike
    /// <see cref="UpdateAsync"/>'s, it may be left out: a new field has no roll-up to lose.
    /// </summary>
    Task<CustomFieldDefinition> CreateAsync(
        Guid actorUserId,
        Guid typeId,
        string name,
        CustomFieldKind kind,
        IReadOnlyList<string> optionNames,
        bool rollsUp = false,
        CancellationToken ct = default);

    /// <summary>
    /// Renames the field, replaces its option list (see <see cref="CustomFieldDefinition.ReviseOptions"/>)
    /// and turns its roll-up on or off. Items that chose a removed option lose that choice,
    /// recorded in their history.
    /// </summary>
    Task<CustomFieldDefinition> UpdateAsync(
        Guid actorUserId,
        Guid id,
        string name,
        IReadOnlyList<CustomFieldOptionRevision> options,
        bool rollsUp,
        CancellationToken ct = default);

    /// <summary><paramref name="orderedIds"/> must list every field of the Type exactly once.</summary>
    Task<IReadOnlyList<CustomFieldDefinition>> ReorderAsync(
        Guid actorUserId,
        Guid typeId,
        IReadOnlyList<Guid> orderedIds,
        CancellationToken ct = default);

    /// <summary>
    /// Removes the field and, from every item of its Type, its value. Each removed value is
    /// recorded in that item's history, so it can still be read there.
    /// </summary>
    Task DeleteAsync(Guid actorUserId, Guid id, CancellationToken ct = default);
}
