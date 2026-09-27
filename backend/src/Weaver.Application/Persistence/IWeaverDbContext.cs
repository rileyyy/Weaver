using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Weaver.Domain;

namespace Weaver.Application.Persistence;

/// <summary>
/// What the application services need from the database: EF Core's own query and
/// change-tracking API, without the provider (Npgsql), the model configuration or the
/// migrations, which stay in Infrastructure. Deliberately not a generic repository: the
/// services compose EF queries directly.
/// </summary>
public interface IWeaverDbContext
{
    DbSet<WorkItem> WorkItems { get; }

    DbSet<Status> Statuses { get; }

    DbSet<Board> Boards { get; }

    DbSet<User> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<WorkItemLayer> WorkItemLayers { get; }

    DbSet<Comment> Comments { get; }

    DbSet<WorkItemLink> WorkItemLinks { get; }

    DbSet<WorkItemRecurrence> WorkItemRecurrences { get; }

    /// <summary>Lets a long-running job discard a failed unit of work and carry on.</summary>
    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
