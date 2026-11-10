using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// The body for adding a field to the end of a Type's. <see cref="Options"/> names a select
/// field's choices, in order; leave it out for other kinds. <see cref="RollsUp"/> can only be
/// true for a number or currency field.
/// </summary>
public record CreateCustomFieldRequest(
    Guid TypeId,
    string Name,
    CustomFieldKind Kind,
    IReadOnlyList<string>? Options = null,
    bool RollsUp = false);
