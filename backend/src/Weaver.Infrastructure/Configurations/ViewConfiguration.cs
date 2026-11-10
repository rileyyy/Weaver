using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class ViewConfiguration : IEntityTypeConfiguration<View>
{
    public void Configure(EntityTypeBuilder<View> builder)
    {
        builder.Property(v => v.Name).IsRequired().HasMaxLength(View.NameMaxLength);
        builder.HasIndex(v => v.Name).IsUnique();
        builder.Property(v => v.SpaceIds).HasColumnType("uuid[]").IsRequired();
        builder.Property(v => v.Text).HasMaxLength(View.TextMaxLength);
        builder.Property(v => v.Tags).HasColumnType("text[]").IsRequired();
        builder.Property(v => v.TagMatch).HasConversion<string>().HasMaxLength(10);
        builder.Property(v => v.Layout).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(v => v.IsBuiltIn);
    }
}
