using Weaver.Domain;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

public static class TestSpaces
{
    /// <summary>Adds a Space with the defaults a new Space gets (<see cref="SpaceDefaults"/>).</summary>
    public static TestSpace Add(WeaverDbContext db, string name = "Home")
    {
        var space = Space.Create(name);
        var statuses = SpaceDefaults.StatusesFor(space.Id);
        var priorities = SpaceDefaults.PrioritiesFor(space.Id);
        var types = SpaceDefaults.TypesFor(space.Id);
        db.Spaces.Add(space);
        db.Statuses.AddRange(statuses);
        db.WorkItemPriorities.AddRange(priorities);
        db.WorkItemTypes.AddRange(types);
        db.SaveChanges();
        return new TestSpace(space, statuses, priorities, types);
    }
}
