using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// A saved View, or the built-in Overview (<see cref="BuiltIn"/>), which can't be changed or
/// deleted. An empty <see cref="SpaceIds"/> means every Space. A View keeps no time range: the
/// window is chosen when it's shown or run.
/// </summary>
public record ViewDto(
    Guid Id,
    string Name,
    bool BuiltIn,
    IReadOnlyList<Guid> SpaceIds,
    string? Text,
    IReadOnlyList<string> Tags,
    TagMatch TagMatch,
    bool ApplyDoneHiding,
    bool ScheduledOrInProgressOnly,
    ViewLayout Layout)
{
    public static ViewDto FromEntity(View view) => new(
        view.Id,
        view.Name,
        view.IsBuiltIn,
        view.SpaceIds,
        view.Text,
        view.Tags,
        view.TagMatch,
        view.ApplyDoneHiding,
        view.ScheduledOrInProgressOnly,
        view.Layout);
}
