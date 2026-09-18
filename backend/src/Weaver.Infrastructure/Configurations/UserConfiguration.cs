using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Username).IsRequired().HasMaxLength(50);
        builder.Property(u => u.NormalizedUsername).IsRequired().HasMaxLength(50);
        builder.Property(u => u.PasswordHash).IsRequired();

        builder.HasIndex(u => u.NormalizedUsername).IsUnique();
    }
}
