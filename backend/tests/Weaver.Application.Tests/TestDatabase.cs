using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Weaver.Infrastructure;

namespace Weaver.Application.Tests;

/// <summary>
/// A fresh InMemory database per test, with the same timestamp interceptor the real
/// context gets, so services behave as they do in production.
/// </summary>
public static class TestDatabase
{
    public static WeaverDbContext Create(TimeProvider clock)
    {
        var options = new DbContextOptionsBuilder<WeaverDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new TimestampInterceptor(clock))
            // InMemory has no transactions; the services' commit/rollback is covered against Postgres.
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var db = new WeaverDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
