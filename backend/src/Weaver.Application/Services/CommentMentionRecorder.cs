using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>
/// Keeps a comment's <see cref="CommentMention"/> rows in step with its text. It only stages the
/// changes; the caller saves them with the comment, so the two never disagree.
/// </summary>
internal sealed class CommentMentionRecorder(IWeaverDbContext db)
{
    private readonly IWeaverDbContext _db = db;

    /// <summary>
    /// Adds a row for each account newly mentioned and removes the rows of those no longer
    /// mentioned. A mention the comment already had keeps its row, and so its time: an edit
    /// doesn't ask the mentioned account again. Handles that name no account are ignored.
    /// </summary>
    public async Task RecordAsync(Comment comment, CancellationToken ct)
    {
        var mentionedIds = await MentionedAccountIdsAsync(comment.Body, ct);
        var existing = await _db.CommentMentions
            .Where(m => m.CommentId == comment.Id)
            .ToListAsync(ct);

        _db.CommentMentions.RemoveRange(existing.Where(m => !mentionedIds.Contains(m.MentionedUserId)));

        var alreadyMentioned = existing.Select(m => m.MentionedUserId).ToHashSet();
        _db.CommentMentions.AddRange(mentionedIds
            .Where(id => !alreadyMentioned.Contains(id))
            .Select(id => CommentMention.Create(comment.Id, id)));
    }

    private async Task<HashSet<Guid>> MentionedAccountIdsAsync(string body, CancellationToken ct)
    {
        var handles = Mentions.Parse(body).ToList();
        if (handles.Count == 0)
        {
            return [];
        }

        var ids = await _db.Users
            .Where(u => u.Handle != null && handles.Contains(u.Handle))
            .Select(u => u.Id)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }
}
