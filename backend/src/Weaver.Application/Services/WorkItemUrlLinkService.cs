using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class WorkItemUrlLinkService(IWeaverDbContext db, ICallerContext caller) : IWorkItemUrlLinkService
{
    private static readonly Expression<Func<WorkItemUrlLink, WorkItemUrlLinkView>> ViewProjection =
        l => new WorkItemUrlLinkView(l.Id, l.Url, l.Label);

    // The same projection for a link already in memory, so a new field reaches both the list and
    // the created link.
    private static readonly Func<WorkItemUrlLink, WorkItemUrlLinkView> ToView = ViewProjection.Compile();

    private readonly IWeaverDbContext _db = db;
    private readonly WorkItemHistoryRecorder _history = new WorkItemHistoryRecorder(db, caller);

    public async Task<IReadOnlyList<WorkItemUrlLinkView>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default) =>
        await _db.WorkItemUrlLinks
            .Where(l => l.WorkItemId == workItemId)
            .OrderBy(l => l.CreatedAtUtc)
            .Select(ViewProjection)
            .ToListAsync(ct);

    public async Task<WorkItemUrlLinkView> CreateAsync(Guid workItemId, string url, string? label, CancellationToken ct = default)
    {
        var link = WorkItemUrlLink.Create(workItemId, url, label);

        await _db.EnsureWorkItemExistsAsync(workItemId, ct);

        _db.WorkItemUrlLinks.Add(link);
        _history.Record(workItemId, WorkItemHistoryField.UrlLink, null, UrlLinkHistoryValue.Of(link));
        await _db.SaveChangesAsync(ct);
        return ToView(link);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var link = await _db.WorkItemUrlLinks.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new EntityNotFoundException(nameof(WorkItemUrlLink), id);

        _db.WorkItemUrlLinks.Remove(link);
        _history.Record(link.WorkItemId, WorkItemHistoryField.UrlLink, UrlLinkHistoryValue.Of(link), null);
        await _db.SaveChangesAsync(ct);
    }
}
