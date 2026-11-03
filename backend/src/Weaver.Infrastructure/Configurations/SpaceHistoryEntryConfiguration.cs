using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class SpaceHistoryEntryConfiguration : IEntityTypeConfiguration<SpaceHistoryEntry>
{
    public void Configure(EntityTypeBuilder<SpaceHistoryEntry> builder)
    {
        builder.ToTable("SpaceHistory");

        builder.Property(e => e.Id).UseIdentityAlwaysColumn();
        builder.Property(e => e.Field).IsRequired().HasMaxLength(50);
        builder.Property(e => e.ActorLabel).HasMaxLength(HistoryActor.ActorLabelMaxLength);
        builder.Property(e => e.OldValue).HasColumnType("jsonb");
        builder.Property(e => e.NewValue).HasColumnType("jsonb");

        builder.HasIndex(e => new { e.SpaceId, e.Id });

        // Restrict for the same reason as work item history: "who" never silently disappears.
        builder.HasOne<Space>()
            .WithMany()
            .HasForeignKey(e => e.SpaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApiKey>()
            .WithMany()
            .HasForeignKey(e => e.ApiKeyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
