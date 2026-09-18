using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.Property(w => w.Title).IsRequired().HasMaxLength(500);

        builder.HasOne(w => w.Parent)
            .WithMany(w => w.Children)
            .HasForeignKey(w => w.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Status)
            .WithMany()
            .HasForeignKey(w => w.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        // A board cell is (ParentId, StatusId); rank only needs to sort within that cell.
        builder.HasIndex(w => new { w.ParentId, w.StatusId, w.Rank });

        builder.Property(w => w.Version)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");
    }
}
