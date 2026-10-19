using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class WorkItemLayerConfiguration : IEntityTypeConfiguration<WorkItemLayer>
{
    public static readonly Guid ProjectId = Guid.Parse("00000000-0000-0000-0000-000000000101");
    public static readonly Guid GoalId = Guid.Parse("00000000-0000-0000-0000-000000000102");
    public static readonly Guid TaskId = Guid.Parse("00000000-0000-0000-0000-000000000103");

    public void Configure(EntityTypeBuilder<WorkItemLayer> builder)
    {
        builder.Property(l => l.Name).IsRequired().HasMaxLength(LookupValue.NameMaxLength);
        builder.Property(l => l.Color).IsRequired().HasMaxLength(HexColor.Length);
        builder.HasIndex(l => l.Order).IsUnique();

        // Task keeps the green every card's color bar showed before layers had colors.
        builder.HasData(
            new { Id = ProjectId, Name = "Project", Order = 0, Color = "#D81B60" },
            new { Id = GoalId, Name = "Goal", Order = 1, Color = "#00897B" },
            new { Id = TaskId, Name = "Task", Order = 2, Color = "#43A047" });
    }
}
