using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Weaver.Infrastructure.Tests;

[TestFixture]
public class DatabaseMigrationsTests
{
    [Test]
    public void UnreachableDatabase_FailsWithAnErrorThatSaysMigrationFailed()
    {
        // Nothing listens on port 1, so the connection is refused without needing a database.
        using var services = new ServiceCollection()
            .AddDbContext<WeaverDbContext>(options =>
                options.UseNpgsql("Host=127.0.0.1;Port=1;Database=weaver;Username=weaver;Password=unused;Timeout=5"))
            .BuildServiceProvider();

        var error = Assert.ThrowsAsync<InvalidOperationException>(() => services.MigrateWeaverDatabaseAsync());

        Assert.Multiple(() =>
        {
            Assert.That(error!.Message, Does.Contain("Applying database migrations failed"));
            Assert.That(error.InnerException, Is.Not.Null);
        });
    }
}
