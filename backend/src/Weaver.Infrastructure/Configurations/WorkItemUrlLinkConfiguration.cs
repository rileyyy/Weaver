using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class WorkItemUrlLinkConfiguration : IEntityTypeConfiguration<WorkItemUrlLink>
{
    public void Configure(EntityTypeBuilder<WorkItemUrlLink> builder)
    {
        builder.Property(l => l.Url).IsRequired().HasMaxLength(WorkItemUrlLink.UrlMaxLength);
        builder.Property(l => l.Label).HasMaxLength(WorkItemUrlLink.LabelMaxLength);

        builder.HasIndex(l => l.WorkItemId);

        // Cascade like comments: a URL link belongs to its one item, so purging the item
        // takes its URL links with it.
        builder.HasOne(l => l.WorkItem)
            .WithMany()
            .HasForeignKey(l => l.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Matches WorkItem's Trash filter: a trashed item's URL links are kept, but hidden with it.
        builder.HasQueryFilter(l => l.WorkItem!.TrashedAtUtc == null);
    }
}
