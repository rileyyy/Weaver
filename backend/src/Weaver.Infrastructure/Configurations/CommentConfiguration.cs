using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.Property(c => c.Body).IsRequired().HasMaxLength(Comment.BodyMaxLength);

        builder.HasIndex(c => c.WorkItemId);

        builder.HasOne(c => c.WorkItem)
            .WithMany()
            .HasForeignKey(c => c.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Matches WorkItem's Trash filter: a trashed item's comments are kept, but hidden with it.
        builder.HasQueryFilter(c => c.WorkItem!.TrashedAtUtc == null);

        builder.HasOne(c => c.AuthorUser)
            .WithMany()
            .HasForeignKey(c => c.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
