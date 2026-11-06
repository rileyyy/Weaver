using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class WorkItemAttachmentConfiguration : IEntityTypeConfiguration<WorkItemAttachment>
{
    public void Configure(EntityTypeBuilder<WorkItemAttachment> builder)
    {
        // The id is chosen before the row is saved, since the file is written under it first.
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.FileName).IsRequired().HasMaxLength(WorkItemAttachment.FileNameMaxLength);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(WorkItemAttachment.ContentTypeMaxLength);

        builder.HasIndex(a => a.WorkItemId);

        // Cascade like URL links: purging the item (ticket 20) takes its attachment rows with
        // it. Its files are removed through IAttachmentFileCleanup, which purge runs through.
        builder.HasOne(a => a.WorkItem)
            .WithMany()
            .HasForeignKey(a => a.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Matches WorkItem's Trash filter: a trashed item's attachments are kept, but hidden with it.
        builder.HasQueryFilter(a => a.WorkItem!.TrashedAtUtc == null);

        // An uploader's account rarely goes (history holds it back), but if it does the file
        // stays with the item.
        builder.HasOne(a => a.UploadedByUser)
            .WithMany()
            .HasForeignKey(a => a.UploadedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
