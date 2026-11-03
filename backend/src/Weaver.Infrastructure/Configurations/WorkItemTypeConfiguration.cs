using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class WorkItemTypeConfiguration : IEntityTypeConfiguration<WorkItemType>
{
    public void Configure(EntityTypeBuilder<WorkItemType> builder)
    {
        builder.Property(t => t.Name).IsRequired().HasMaxLength(LookupValue.NameMaxLength);
        builder.Property(t => t.Color).IsRequired().HasMaxLength(HexColor.Length);

        // Restrict, and no seed data, for the same reasons as StatusConfiguration.
        builder.HasOne<Space>()
            .WithMany()
            .HasForeignKey(t => t.SpaceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.SpaceId, t.Order }).IsUnique();
    }
}
