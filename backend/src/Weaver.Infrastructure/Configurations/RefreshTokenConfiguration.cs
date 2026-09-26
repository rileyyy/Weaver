using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(64);

        builder.HasIndex(t => t.TokenHash).IsUnique();

        // Two parallel refreshes with the same token must not both mint a
        // new pair: the second SaveChanges fails on the stale xmin.
        builder.Property(t => t.Version)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
