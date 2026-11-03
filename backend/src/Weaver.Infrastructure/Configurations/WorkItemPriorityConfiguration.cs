using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class WorkItemPriorityConfiguration : IEntityTypeConfiguration<WorkItemPriority>
{
    public void Configure(EntityTypeBuilder<WorkItemPriority> builder)
    {
        builder.Property(p => p.Name).IsRequired().HasMaxLength(LookupValue.NameMaxLength);

        // Restrict, and no seed data, for the same reasons as StatusConfiguration.
        builder.HasOne<Space>()
            .WithMany()
            .HasForeignKey(p => p.SpaceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.SpaceId, p.Order }).IsUnique();
    }
}
