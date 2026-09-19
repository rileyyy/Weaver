using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class StatusConfiguration : IEntityTypeConfiguration<Status>
{
    public static readonly Guid ToDoId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid DoingId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid DoneId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    public void Configure(EntityTypeBuilder<Status> builder)
    {
        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.Property(s => s.Color).IsRequired().HasMaxLength(7);
        builder.HasIndex(s => s.Order).IsUnique();

        // Every WorkItem requires a StatusId, so a fresh database needs at least
        // these to be usable out of the box; teams can rename/reorder/add more.
        // Colors are just sensible defaults — stored per-row (not derived from
        // Category) specifically so they can be reconfigured later without a
        // schema change.
        builder.HasData(
            new Status { Id = ToDoId, Name = "To Do", Order = 0, Category = StatusCategory.ToDo, Color = "#1E88E5" },
            new Status { Id = DoingId, Name = "Doing", Order = 1, Category = StatusCategory.Doing, Color = "#FB8C00" },
            new Status { Id = DoneId, Name = "Done", Order = 2, Category = StatusCategory.Done, Color = "#7B1FA2" });
    }
}
