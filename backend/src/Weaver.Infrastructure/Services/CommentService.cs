using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Infrastructure.Services;

public class CommentService : ICommentService
{
    private readonly WeaverDbContext _db;

    public CommentService(WeaverDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Comment>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default) =>
        await _db.Comments
            .Include(c => c.AuthorUser)
            .Where(c => c.WorkItemId == workItemId)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<Comment> CreateAsync(Guid workItemId, Guid authorUserId, string body, CancellationToken ct = default)
    {
        TextValidation.RequireText(body, "Comment", Comment.BodyMaxLength);

        if (!await _db.WorkItems.AnyAsync(w => w.Id == workItemId, ct))
        {
            throw new EntityNotFoundException(nameof(WorkItem), workItemId);
        }

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            AuthorUserId = authorUserId,
            Body = body,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync(ct);
        await _db.Entry(comment).Reference(c => c.AuthorUser).LoadAsync(ct);
        return comment;
    }

    public async Task<Comment> UpdateAsync(Guid id, Guid requestingUserId, string body, CancellationToken ct = default)
    {
        TextValidation.RequireText(body, "Comment", Comment.BodyMaxLength);

        var comment = await _db.Comments.Include(c => c.AuthorUser).FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new EntityNotFoundException(nameof(Comment), id);

        if (comment.AuthorUserId != requestingUserId)
        {
            throw new CommentAuthorMismatchException(id);
        }

        comment.Body = body;
        comment.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return comment;
    }

    public async Task DeleteAsync(Guid id, Guid requestingUserId, CancellationToken ct = default)
    {
        var comment = await _db.Comments.FindAsync([id], ct)
            ?? throw new EntityNotFoundException(nameof(Comment), id);

        if (comment.AuthorUserId != requestingUserId)
        {
            throw new CommentAuthorMismatchException(id);
        }

        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync(ct);
    }
}
