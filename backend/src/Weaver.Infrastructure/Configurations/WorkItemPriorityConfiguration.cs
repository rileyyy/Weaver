using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class WorkItemPriorityConfiguration : IEntityTypeConfiguration<WorkItemPriority>
{
    public static readonly Guid LowId = Guid.Parse("00000000-0000-0000-0000-000000000201");
    public static readonly Guid MediumId = Guid.Parse("00000000-0000-0000-0000-000000000202");
    public static readonly Guid HighId = Guid.Parse("00000000-0000-0000-0000-000000000203");
    public static readonly Guid UrgentId = Guid.Parse("00000000-0000-0000-0000-000000000204");

    public void Configure(EntityTypeBuilder<WorkItemPriority> builder)
    {
        builder.Property(p => p.Name).IsRequired().HasMaxLength(LookupValue.NameMaxLength);
        builder.HasIndex(p => p.Order).IsUnique();

        // The four values the former WorkItemPriority enum had. The migration that replaced
        // the enum maps its stored ordinals onto these ids, so they must not change.
        builder.HasData(
            new { Id = LowId, Name = "Low", Order = 0 },
            new { Id = MediumId, Name = "Medium", Order = 1 },
            new { Id = HighId, Name = "High", Order = 2 },
            new { Id = UrgentId, Name = "Urgent", Order = 3 });
    }
}
