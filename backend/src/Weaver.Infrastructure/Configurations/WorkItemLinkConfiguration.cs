using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class WorkItemLinkConfiguration : IEntityTypeConfiguration<WorkItemLink>
{
    public void Configure(EntityTypeBuilder<WorkItemLink> builder)
    {
        builder.HasIndex(l => l.WorkItemId);
        builder.HasIndex(l => l.LinkedWorkItemId);

        // Restrict (not Cascade) on both sides — a work item with active
        // links can't be silently orphaned by deleting the item on the
        // other end. Two Restrict FKs to the same principal table is fine;
        // it's only Cascade/SetNull chains EF rejects as multiple paths.
        builder.HasOne(l => l.WorkItem)
            .WithMany()
            .HasForeignKey(l => l.WorkItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.LinkedWorkItem)
            .WithMany()
            .HasForeignKey(l => l.LinkedWorkItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
