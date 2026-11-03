namespace Weaver.Domain;

/// <summary>
/// A change to a work item's descriptive fields. A field left <see cref="Optional{T}.Unset"/>
/// is unchanged; one set to <c>null</c> is cleared. The title can't be cleared.
/// </summary>
public record WorkItemDetailsPatch
{
    public Optional<string> Title { get; init; }

    public Optional<string?> Description { get; init; }

    public Optional<Guid?> LayerId { get; init; }

    public Optional<Guid?> PriorityId { get; init; }

    /// <summary>A blank When clears it, like <c>null</c>.</summary>
    public Optional<string?> When { get; init; }
}
