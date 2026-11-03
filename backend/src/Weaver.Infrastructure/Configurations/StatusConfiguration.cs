using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class StatusConfiguration : IEntityTypeConfiguration<Status>
{
    public void Configure(EntityTypeBuilder<Status> builder)
    {
        builder.Property(s => s.Name).IsRequired().HasMaxLength(LookupValue.NameMaxLength);
        builder.Property(s => s.Color).IsRequired().HasMaxLength(HexColor.Length);

        // Restrict: a Space can't vanish from under its own columns. Nothing seeds statuses here:
        // each Space gets its defaults when it's created (SpaceDefaults).
        builder.HasOne<Space>()
            .WithMany()
            .HasForeignKey(s => s.SpaceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.SpaceId, s.Order }).IsUnique();
    }
}
