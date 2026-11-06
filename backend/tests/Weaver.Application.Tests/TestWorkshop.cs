using Weaver.Domain;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>A Space configured unlike the defaults: only Done, Task and High carry over by name.</summary>
public sealed record TestWorkshop(Guid Id, Guid Quoted, Guid Booked, Guid Done, Guid Task, Guid Chore, Guid High, Guid Whenever)
{
    public static TestWorkshop Add(WeaverDbContext db)
    {
        var space = Space.Create("Workshop");
        var quoted = Status.Create(space.Id, "Quoted", 0, StatusCategory.ToDo, "#111111");
        var booked = Status.Create(space.Id, "Booked", 1, StatusCategory.Doing, "#222222");
        var done = Status.Create(space.Id, "Done", 2, StatusCategory.Done, "#333333");
        var chore = WorkItemType.Create(space.Id, "Chore", 0, "#444444");
        var task = WorkItemType.Create(space.Id, "task", 1, "#555555");
        var whenever = WorkItemPriority.Create(space.Id, "Whenever", 0);
        var high = WorkItemPriority.Create(space.Id, "High", 1);
        db.Spaces.Add(space);
        db.Statuses.AddRange(quoted, booked, done);
        db.WorkItemTypes.AddRange(chore, task);
        db.WorkItemPriorities.AddRange(whenever, high);
        db.SaveChanges();
        return new TestWorkshop(space.Id, quoted.Id, booked.Id, done.Id, task.Id, chore.Id, high.Id, whenever.Id);
    }
}
