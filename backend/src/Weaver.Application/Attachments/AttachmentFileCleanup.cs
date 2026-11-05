using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;

namespace Weaver.Application.Attachments;

public class AttachmentFileCleanup(IWeaverDbContext db, IAttachmentStore store) : IAttachmentFileCleanup
{
    private readonly IWeaverDbContext _db = db;
    private readonly IAttachmentStore _store = store;

    public async Task DeleteFilesForWorkItemsAsync(IReadOnlyCollection<Guid> workItemIds, CancellationToken ct = default)
    {
        // Purge only ever takes trashed items, which the Trash query filter hides.
        var attachmentIds = await _db.WorkItemAttachments
            .IgnoreQueryFilters()
            .Where(a => workItemIds.Contains(a.WorkItemId))
            .Select(a => a.Id)
            .ToListAsync(ct);

        foreach (var id in attachmentIds)
        {
            await _store.DeleteAsync(id, ct);
        }
    }
}
