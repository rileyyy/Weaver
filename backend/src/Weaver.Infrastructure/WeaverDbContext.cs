using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure.Configurations;

namespace Weaver.Infrastructure;

public class WeaverDbContext(DbContextOptions<WeaverDbContext> options) : DbContext(options), IWeaverDbContext
{
    public DbSet<Space> Spaces => Set<Space>();

    public DbSet<SpaceHistoryEntry> SpaceHistory => Set<SpaceHistoryEntry>();

    public DbSet<View> Views => Set<View>();

    public DbSet<DefaultView> DefaultViews => Set<DefaultView>();

    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    public DbSet<Status> Statuses => Set<Status>();

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    public DbSet<WorkItemType> WorkItemTypes => Set<WorkItemType>();

    public DbSet<CustomFieldDefinition> CustomFields => Set<CustomFieldDefinition>();

    public DbSet<WorkItemPriority> WorkItemPriorities => Set<WorkItemPriority>();

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<WorkItemLink> WorkItemLinks => Set<WorkItemLink>();

    public DbSet<WorkItemUrlLink> WorkItemUrlLinks => Set<WorkItemUrlLink>();

    public DbSet<WorkItemAttachment> WorkItemAttachments => Set<WorkItemAttachment>();

    public DbSet<WorkItemRecurrence> WorkItemRecurrences => Set<WorkItemRecurrence>();

    public DbSet<ServerSettings> ServerSettings => Set<ServerSettings>();

    public DbSet<WorkItemHistoryEntry> WorkItemHistory => Set<WorkItemHistoryEntry>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

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
        modelBuilder.ApplyConfiguration(new SpaceConfiguration());
        modelBuilder.ApplyConfiguration(new SpaceHistoryEntryConfiguration());
        modelBuilder.ApplyConfiguration(new ViewConfiguration());
        modelBuilder.ApplyConfiguration(new DefaultViewConfiguration());
        modelBuilder.ApplyConfiguration(new WorkItemConfiguration());
        modelBuilder.ApplyConfiguration(new StatusConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new ApiKeyConfiguration());
        modelBuilder.ApplyConfiguration(new WorkItemTypeConfiguration());
        modelBuilder.ApplyConfiguration(new CustomFieldDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new WorkItemPriorityConfiguration());
        modelBuilder.ApplyConfiguration(new CommentConfiguration());
        modelBuilder.ApplyConfiguration(new WorkItemLinkConfiguration());
        modelBuilder.ApplyConfiguration(new WorkItemUrlLinkConfiguration());
        modelBuilder.ApplyConfiguration(new WorkItemAttachmentConfiguration());
        modelBuilder.ApplyConfiguration(new WorkItemRecurrenceConfiguration());
        modelBuilder.ApplyConfiguration(new ServerSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new WorkItemHistoryEntryConfiguration());
    }
}
