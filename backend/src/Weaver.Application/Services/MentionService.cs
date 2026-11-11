using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

public class MentionService(IWeaverDbContext db, ICallerContext caller) : IMentionService
{
    private readonly IWeaverDbContext _db = db;
    private readonly ICallerContext _caller = caller;

    public async Task<IReadOnlyList<PendingMention>> ListPendingAsync(CancellationToken ct = default)
    {
        var me = _caller.Current.AccountId;
        return await _db.CommentMentions
            .Where(m => m.MentionedUserId == me)
            .Where(m => m.Comment!.AuthorUser!.Kind != UserKind.Agent)
            .Where(m => !_db.Comments.Any(reply =>
                reply.WorkItemId == m.Comment!.WorkItemId
                && reply.AuthorUserId == me
                && reply.CreatedAtUtc > m.CreatedAtUtc))
            .OrderBy(m => m.CreatedAtUtc)
            .ThenBy(m => m.CommentId)
            .Select(m => new PendingMention(
                new CommentView(
                    m.Comment!.Id,
                    m.Comment.WorkItemId,
                    m.Comment.AuthorUserId,
                    m.Comment.AuthorUser!.Username,
                    m.Comment.Body,
                    m.Comment.CreatedAtUtc,
                    m.Comment.UpdatedAtUtc),
                new WorkItemReference(m.Comment.WorkItem!.Number, m.Comment.WorkItem.Title),
                m.CreatedAtUtc))
            .ToListAsync(ct);
    }
}
