using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class WorkItemHistoryEntryConfiguration : IEntityTypeConfiguration<WorkItemHistoryEntry>
{
    public void Configure(EntityTypeBuilder<WorkItemHistoryEntry> builder)
    {
        builder.ToTable("WorkItemHistory");

        builder.Property(e => e.Id).UseIdentityAlwaysColumn();
        builder.Property(e => e.Field).IsRequired().HasMaxLength(50);
        builder.Property(e => e.ActorLabel).HasMaxLength(HistoryActor.ActorLabelMaxLength);
        builder.Property(e => e.OldValue).HasColumnType("jsonb");
        builder.Property(e => e.NewValue).HasColumnType("jsonb");

        // No foreign key to WorkItems: the history of a deleted item must survive it.
        builder.HasIndex(e => new { e.WorkItemId, e.Id });

        // "Changes since T" (ticket 31) reads across every item by time.
        builder.HasIndex(e => e.CreatedAtUtc);

        // Restrict, like a comment's author: an account (or an agent's key) with history can't
        // be hard-deleted, only deactivated or revoked, so "who" never silently disappears.
        builder.HasOne(e => e.Account)
            .WithMany()
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ApiKey)
            .WithMany()
            .HasForeignKey(e => e.ApiKeyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
