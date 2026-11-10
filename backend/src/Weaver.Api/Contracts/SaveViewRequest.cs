using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// A View's whole definition, for creating one or replacing one's. Leave <see cref="SpaceIds"/>
/// empty for every Space.
/// </summary>
public record SaveViewRequest(
    string Name,
    IReadOnlyList<Guid>? SpaceIds = null,
    string? Text = null,
    IReadOnlyList<string>? Tags = null,
    TagMatch TagMatch = TagMatch.Any,
    bool ApplyDoneHiding = true,
    bool ScheduledOrInProgressOnly = false,
    ViewLayout Layout = ViewLayout.Board)
{
    public ViewDefinition ToDefinition() => new(
        Name,
        SpaceIds ?? [],
        Text,
        Tags ?? [],
        TagMatch,
        ApplyDoneHiding,
        ScheduledOrInProgressOnly,
        Layout);
}
