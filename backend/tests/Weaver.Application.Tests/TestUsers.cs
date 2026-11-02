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

    /// <summary>
    /// An agent row with <c>IsAdmin</c> set, as one could be before <see cref="User.GrantAdmin"/>
    /// refused agents. The flag is written directly because no domain method allows it any more.
    /// </summary>
    public static User AddAgentFlaggedAsAdmin(WeaverDbContext db, string name)
    {
        var agent = User.CreateAgent(name, null);
        db.Users.Add(agent);
        db.Entry(agent).Property(u => u.IsAdmin).CurrentValue = true;
        db.SaveChanges();
        return agent;
    }
}
