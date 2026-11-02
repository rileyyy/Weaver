using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.Property(k => k.Label).IsRequired().HasMaxLength(ApiKey.MaxLabelLength);
        builder.Property(k => k.KeyHash).IsRequired().HasMaxLength(64);

        // Every key-authenticated request looks its key up by hash.
        builder.HasIndex(k => k.KeyHash).IsUnique();

        builder.HasOne(k => k.User)
            .WithMany()
            .HasForeignKey(k => k.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
