using Weaver.Domain;

namespace Weaver.Application.Tests;

/// <summary>A Space added by <see cref="TestSpaces"/>, with its default statuses, priorities and Types by name.</summary>
public sealed class TestSpace(
    Space space,
    IReadOnlyList<Status> statuses,
    IReadOnlyList<WorkItemPriority> priorities,
    IReadOnlyList<WorkItemType> types)
{
    public Space Space { get; } = space;

    public Guid Id => Space.Id;

    public string Name => Space.Name;

    public Guid ToDo { get; } = IdOf(statuses, "To Do");

    public Guid Doing { get; } = IdOf(statuses, "Doing");

    public Guid Done { get; } = IdOf(statuses, "Done");

    public Guid Low { get; } = IdOf(priorities, "Low");

    public Guid Medium { get; } = IdOf(priorities, "Medium");

    public Guid High { get; } = IdOf(priorities, "High");

    public Guid Urgent { get; } = IdOf(priorities, "Urgent");

    public Guid Project { get; } = IdOf(types, "Project");

    public Guid Goal { get; } = IdOf(types, "Goal");

    public Guid Task { get; } = IdOf(types, "Task");

    private static Guid IdOf(IEnumerable<LookupValue> values, string name) => values.Single(v => v.Name == name).Id;
}
