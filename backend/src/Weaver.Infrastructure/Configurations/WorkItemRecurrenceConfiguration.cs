using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class WorkItemRecurrenceConfiguration : IEntityTypeConfiguration<WorkItemRecurrence>
{
    public void Configure(EntityTypeBuilder<WorkItemRecurrence> builder)
    {
        builder.HasKey(r => r.WorkItemId);

        builder.HasOne(r => r.WorkItem)
            .WithOne()
            .HasForeignKey<WorkItemRecurrence>(r => r.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Matches WorkItem's Trash filter: a trashed item's schedule is hidden with it, which
        // also keeps background generation from copying a trashed template.
        builder.HasQueryFilter(r => r.WorkItem!.TrashedAtUtc == null);

        builder.Ignore(r => r.Schedule);
    }
}
