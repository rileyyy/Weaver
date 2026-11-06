using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Weaver.Application.Attachments;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class WorkItemAttachmentService(
    IWeaverDbContext db,
    ICallerContext caller,
    IAttachmentStore store,
    IOptions<AttachmentOptions> options) : IWorkItemAttachmentService
{
    private static readonly Expression<Func<WorkItemAttachment, WorkItemAttachmentView>> ViewProjection =
        a => new WorkItemAttachmentView(a.Id, a.WorkItemId, a.FileName, a.ContentType, a.SizeBytes, a.CreatedAtUtc);

    // The same projection for an attachment already in memory, so a new field reaches both.
    private static readonly Func<WorkItemAttachment, WorkItemAttachmentView> ToView = ViewProjection.Compile();

    private readonly IWeaverDbContext _db = db;
    private readonly ICallerContext _caller = caller;
    private readonly IAttachmentStore _store = store;
    private readonly AttachmentOptions _options = options.Value;
    private readonly WorkItemHistoryRecorder _history = new WorkItemHistoryRecorder(db, caller);

    public async Task<IReadOnlyList<WorkItemAttachmentView>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default) =>
        await _db.WorkItemAttachments
            .Where(a => a.WorkItemId == workItemId)
            .OrderBy(a => a.CreatedAtUtc)
            .Select(ViewProjection)
            .ToListAsync(ct);

    public async Task<WorkItemAttachmentView> AddAsync(
        Guid workItemId,
        string? fileName,
        string? contentType,
        Stream content,
        CancellationToken ct = default)
    {
        await _db.EnsureWorkItemExistsAsync(workItemId, ct);

        // The file is written first, under an id chosen now, so a row is only ever saved for a
        // file that's fully stored; every failure after the write removes the file again.
        var id = Guid.NewGuid();
        var limited = new SizeLimitedStream(content, _options.MaxFileSizeBytes, _options.MaxFileSizeMegabytes);
        await _store.WriteAsync(id, limited, ct);

        try
        {
            var attachment = WorkItemAttachment.Create(
                id, workItemId, fileName, contentType, limited.BytesRead, _caller.Current.AccountId);
            _db.WorkItemAttachments.Add(attachment);
            _history.Record(workItemId, WorkItemHistoryField.Attachment, null, AttachmentHistoryValue.Of(attachment));
            await _db.SaveChangesAsync(ct);
            return ToView(attachment);
        }
        catch
        {
            // Not the request's token: a cancelled request must still clean up.
            await _store.DeleteAsync(id, CancellationToken.None);
            throw;
        }
    }

    public async Task<AttachmentContent> OpenContentAsync(Guid id, CancellationToken ct = default)
    {
        var attachment = await _db.WorkItemAttachments
            .Where(a => a.Id == id)
            .Select(ViewProjection)
            .FirstOrDefaultAsync(ct)
            ?? throw new EntityNotFoundException(nameof(WorkItemAttachment), id);

        return new AttachmentContent(attachment, await _store.OpenReadAsync(id, ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var attachment = await _db.WorkItemAttachments.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new EntityNotFoundException(nameof(WorkItemAttachment), id);

        _db.WorkItemAttachments.Remove(attachment);
        _history.Record(attachment.WorkItemId, WorkItemHistoryField.Attachment, AttachmentHistoryValue.Of(attachment), null);
        await _db.SaveChangesAsync(ct);

        // Only after the commit: a file left behind by a failed delete is wasted space, while a
        // row whose file is gone would be a broken download.
        await _store.DeleteAsync(id, CancellationToken.None);
    }
}
