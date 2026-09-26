using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.Property(w => w.Title).IsRequired().HasMaxLength(WorkItem.TitleMaxLength);

        // Database-generated (Postgres identity column), never set by
        // application code — see WorkItem.Number's own doc comment. Also
        // works against EF Core's in-memory provider for tests: it honors
        // plain ValueGeneratedOnAdd for integer properties regardless of
        // the Npgsql-specific generation strategy this also sets.
        builder.Property(w => w.Number).UseIdentityAlwaysColumn();
        builder.HasIndex(w => w.Number).IsUnique();

        builder.HasOne(w => w.Parent)
            .WithMany(w => w.Children)
            .HasForeignKey(w => w.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Status)
            .WithMany()
            .HasForeignKey(w => w.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Layer)
            .WithMany()
            .HasForeignKey(w => w.LayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.AssignedToUser)
            .WithMany()
            .HasForeignKey(w => w.AssignedToUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Native Postgres array column (Npgsql maps List<string> to text[]
        // directly) — no join table, since nothing queries by tag
        // server-side today (tag search/filter is client-side, matching
        // every other board filter).
        builder.Property(w => w.Tags).HasColumnType("text[]").IsRequired();

        // A board cell is (ParentId, StatusId); rank only needs to sort within that cell.
        builder.HasIndex(w => new { w.ParentId, w.StatusId, w.Rank });

        builder.Property(w => w.Version)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");
    }
}
