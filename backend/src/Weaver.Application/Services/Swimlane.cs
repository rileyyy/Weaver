using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// One board swimlane: a direct child of the board's scope and its own direct
/// children (the lane's cards), both ordered by rank.
/// </summary>
public record Swimlane(WorkItem Lane, IReadOnlyList<WorkItem> Cards);
