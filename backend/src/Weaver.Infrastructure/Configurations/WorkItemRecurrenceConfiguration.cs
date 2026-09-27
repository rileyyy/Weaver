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

        builder.Ignore(r => r.Schedule);
    }
}
