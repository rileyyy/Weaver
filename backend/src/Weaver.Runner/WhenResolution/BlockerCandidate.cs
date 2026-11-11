namespace Weaver.Runner.WhenResolution;

/// <summary>
/// An item the runner offers the model as what a When phrase may be waiting for. The model names
/// it by <see cref="Number"/>, which is short and human-facing, rather than by its id.
/// </summary>
public sealed record BlockerCandidate(Guid Id, int Number, string Title, string? SpaceName, string? ParentTitle);
