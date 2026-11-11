using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Weaver.Domain;

namespace Weaver.Infrastructure.Configurations;

public class CommentMentionConfiguration : IEntityTypeConfiguration<CommentMention>
{
    public void Configure(EntityTypeBuilder<CommentMention> builder)
    {
        builder.HasKey(m => new { m.CommentId, m.MentionedUserId });

        // The pending list is read per mentioned account.
        builder.HasIndex(m => m.MentionedUserId);

        builder.HasOne(m => m.Comment)
            .WithMany()
            .HasForeignKey(m => m.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Matches Comment's Trash filter: a trashed item's mentions are kept, but hidden with it.
        builder.HasQueryFilter(m => m.Comment!.WorkItem!.TrashedAtUtc == null);

        // Cascade rather than Restrict: a mention is derived from the comment's text, so it
        // shouldn't stop an account being purged the way the comment's authorship does.
        builder.HasOne(m => m.MentionedUser)
            .WithMany()
            .HasForeignKey(m => m.MentionedUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
