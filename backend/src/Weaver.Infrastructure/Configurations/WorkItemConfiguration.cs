using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.Property(w => w.Title).IsRequired().HasMaxLength(WorkItem.TitleMaxLength);
        builder.Property(w => w.When).HasMaxLength(WorkItem.WhenMaxLength);

        // Database-generated (Postgres identity column), never set by
        // application code — see WorkItem.Number's own doc comment. The EF
        // InMemory provider used by the service tests does not generate it
        // (every item reads 0), so logic must not rely on Number being unique
        // outside Postgres.
        builder.Property(w => w.Number).UseIdentityAlwaysColumn();
        builder.HasIndex(w => w.Number).IsUnique();

        // Restrict: a Space with items in it can't vanish from under them.
        builder.HasOne(w => w.Space)
            .WithMany()
            .HasForeignKey(w => w.SpaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Parent)
            .WithMany(w => w.Children)
            .HasForeignKey(w => w.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Status)
            .WithMany()
            .HasForeignKey(w => w.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Type)
            .WithMany()
            .HasForeignKey(w => w.TypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Priority)
            .WithMany()
            .HasForeignKey(w => w.PriorityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.AssignedToUser)
            .WithMany()
            .HasForeignKey(w => w.AssignedToUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // SetNull: deleting the repeating item keeps the items it generated, which may be
        // needed for reference; they just stop pointing at a template that no longer exists.
        builder.HasOne(w => w.RecurrenceSource)
            .WithMany()
            .HasForeignKey(w => w.RecurrenceSourceId)
            .OnDelete(DeleteBehavior.SetNull);

        // Postgres treats NULLs as distinct, so hand-created items (both columns null) never collide.
        builder.HasIndex(w => new { w.RecurrenceSourceId, w.RecurrenceDate }).IsUnique();

        // Native Postgres array column (Npgsql maps List<string> to text[]
        // directly) — no join table, since nothing queries by tag
        // server-side today (tag search/filter is client-side, matching
        // every other board filter).
        builder.Property(w => w.Tags).HasColumnType("text[]").IsRequired();

        // One document keyed by field id: fields are defined at runtime, so they can't be
        // columns, and validation lives in the Application layer (ADR-0002).
        builder.Property(w => w.CustomFields).HasJsonConversion();

        // A board cell is (SpaceId, ParentId, StatusId); rank only needs to sort within that
        // cell. SpaceId leads so a Space's top-level items (null parent) are one cell of their own.
        builder.HasIndex(w => new { w.SpaceId, w.ParentId, w.StatusId, w.Rank });

        builder.Property(w => w.Version)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");
    }
}
