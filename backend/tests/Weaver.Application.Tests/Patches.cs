using Weaver.Domain;

namespace Weaver.Application.Tests;

/// <summary>Shorthand for the patch arguments the work item service's writes take.</summary>
internal static class Patches
{
    /// <summary>A details patch that sets every field, the way a full edit form does.</summary>
    public static WorkItemDetailsPatch Details(string title, string? description, Guid? layerId, Guid? priorityId) =>
        new() { Title = title, Description = description, LayerId = layerId, PriorityId = priorityId };

    public static Optional<IReadOnlyList<string>> Tags(params string[] tags) => tags;
}
