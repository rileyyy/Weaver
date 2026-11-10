using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

/// <summary>
/// At most one row per user. Both keys cascade: a deleted View sends its users back to Overview,
/// and a deleted user's choice goes with them.
/// </summary>
public class DefaultViewConfiguration : IEntityTypeConfiguration<DefaultView>
{
    public void Configure(EntityTypeBuilder<DefaultView> builder)
    {
        builder.HasKey(d => d.UserId);
        builder.HasOne<User>().WithOne().HasForeignKey<DefaultView>(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<View>().WithMany().HasForeignKey(d => d.ViewId).OnDelete(DeleteBehavior.Cascade);
    }
}
