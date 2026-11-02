using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class CommentService : ICommentService
{
    private readonly IWeaverDbContext _db;
    private readonly TimeProvider _clock;
    private readonly ICallerContext _caller;
    private readonly WorkItemHistoryRecorder _history;

    public CommentService(IWeaverDbContext db, TimeProvider clock, ICallerContext caller)
    {
        _db = db;
        _clock = clock;
        _caller = caller;
        _history = new WorkItemHistoryRecorder(db, caller);
    }

    public async Task<IReadOnlyList<CommentView>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default) =>
        await Project(_db.Comments.Where(c => c.WorkItemId == workItemId).OrderBy(c => c.CreatedAtUtc))
            .ToListAsync(ct);

    public Task<CommentView?> GetAsync(Guid id, CancellationToken ct = default) =>
        Project(_db.Comments.Where(c => c.Id == id)).FirstOrDefaultAsync(ct);

    public async Task<CommentView> CreateAsync(Guid workItemId, string body, CancellationToken ct = default)
    {
        if (!await _db.WorkItems.AnyAsync(w => w.Id == workItemId, ct))
        {
            throw new EntityNotFoundException(nameof(WorkItem), workItemId);
        }

        var comment = Comment.Create(workItemId, _caller.Current.AccountId, body);

        _db.Comments.Add(comment);
        _history.Record(workItemId, WorkItemHistoryField.Comment, null, comment.Body);
        await _db.SaveChangesAsync(ct);
        return await GetViewAsync(comment.Id, ct);
    }

    public async Task<CommentView> UpdateAsync(Guid id, string body, CancellationToken ct = default)
    {
        var comment = await _db.Comments.FindAsync([id], ct)
            ?? throw new EntityNotFoundException(nameof(Comment), id);

        var previousBody = comment.Body;
        comment.Edit(_caller.Current.AccountId, body, _clock.GetUtcNow());
        if (comment.Body != previousBody)
        {
            _history.Record(comment.WorkItemId, WorkItemHistoryField.Comment, previousBody, comment.Body);
        }

        await _db.SaveChangesAsync(ct);
        return await GetViewAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var comment = await _db.Comments.FindAsync([id], ct)
            ?? throw new EntityNotFoundException(nameof(Comment), id);

        comment.EnsureAuthoredBy(_caller.Current.AccountId);

        _db.Comments.Remove(comment);
        _history.Record(comment.WorkItemId, WorkItemHistoryField.Comment, comment.Body, null);
        await _db.SaveChangesAsync(ct);
    }

    private Task<CommentView> GetViewAsync(Guid id, CancellationToken ct) =>
        Project(_db.Comments.Where(c => c.Id == id)).SingleAsync(ct);

    /// <summary>
    /// The author is required (the FK is Restrict), so the inner join never drops a comment and
    /// a missing username can't be papered over with an empty string.
    /// </summary>
    private static IQueryable<CommentView> Project(IQueryable<Comment> comments) =>
        comments.Select(c => new CommentView(
            c.Id,
            c.WorkItemId,
            c.AuthorUserId,
            c.AuthorUser!.Username,
            c.Body,
            c.CreatedAtUtc,
            c.UpdatedAtUtc));
}
