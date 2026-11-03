using Weaver.Domain;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

public static class TestSpaces
{
    public static Space Add(WeaverDbContext db, string name = "Home")
    {
        var space = Space.Create(name);
        db.Spaces.Add(space);
        db.SaveChanges();
        return space;
    }
}
