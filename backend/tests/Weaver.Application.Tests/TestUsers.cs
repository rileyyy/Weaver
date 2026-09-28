using Weaver.Domain;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

public static class TestUsers
{
    public static User Add(WeaverDbContext db, string username, bool isAdmin = false)
    {
        var user = User.CreateHuman(username, username.ToLowerInvariant(), isAdmin);
        user.SetPasswordHash("not-a-real-hash");
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }
}
