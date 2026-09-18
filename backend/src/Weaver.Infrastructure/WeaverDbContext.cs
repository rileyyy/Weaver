using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Infrastructure.Configurations;

namespace Weaver.Infrastructure;

public class WeaverDbContext : DbContext
{
    public WeaverDbContext(DbContextOptions<WeaverDbContext> options) : base(options)
    {
    }

    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    public DbSet<Status> Statuses => Set<Status>();

    public DbSet<Board> Boards => Set<Board>();

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new WorkItemConfiguration());
        modelBuilder.ApplyConfiguration(new StatusConfiguration());
        modelBuilder.ApplyConfiguration(new BoardConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
    }
}
