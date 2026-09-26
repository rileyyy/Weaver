using Microsoft.EntityFrameworkCore;
using Npgsql;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure.Configurations;

namespace Weaver.Infrastructure;

public class WeaverDbContext : DbContext, IWeaverDbContext
{
    public WeaverDbContext(DbContextOptions<WeaverDbContext> options) : base(options)
    {
    }

    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    public DbSet<Status> Statuses => Set<Status>();

    public DbSet<Board> Boards => Set<Board>();

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<WorkItemLayer> WorkItemLayers => Set<WorkItemLayer>();

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<WorkItemLink> WorkItemLinks => Set<WorkItemLink>();

    public DbSet<WorkItemRecurrence> WorkItemRecurrences => Set<WorkItemRecurrence>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateException ex) when (UniqueViolation(ex) is { } violation)
        {
            throw new UniqueConstraintViolationException(violation.ConstraintName, ex);
        }
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException ex) when (UniqueViolation(ex) is { } violation)
        {
            throw new UniqueConstraintViolationException(violation.ConstraintName, ex);
        }
    }

    /// <summary>
    /// Translated here, next to the provider, so the Application layer and the transports
    /// never need to know Postgres error codes.
    /// </summary>
    private static PostgresException? UniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
            ? postgres
            : null;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new WorkItemConfiguration());
        modelBuilder.ApplyConfiguration(new StatusConfiguration());
        modelBuilder.ApplyConfiguration(new BoardConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new WorkItemLayerConfiguration());
        modelBuilder.ApplyConfiguration(new CommentConfiguration());
        modelBuilder.ApplyConfiguration(new WorkItemLinkConfiguration());
        modelBuilder.ApplyConfiguration(new WorkItemRecurrenceConfiguration());
    }
}
