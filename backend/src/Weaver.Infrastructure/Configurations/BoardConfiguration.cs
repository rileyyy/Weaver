using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> builder)
    {
        builder.Property(b => b.Name).IsRequired().HasMaxLength(200);

        builder.HasOne(b => b.ScopeItem)
            .WithMany()
            .HasForeignKey(b => b.ScopeItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
