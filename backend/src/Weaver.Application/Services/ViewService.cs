using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class ViewService(IWeaverDbContext db, ICallerContext caller, IWorkItemQueryService query) : IViewService
{
    private readonly IWeaverDbContext _db = db;
    private readonly ICallerContext _caller = caller;
    private readonly IWorkItemQueryService _query = query;

    public async Task<IReadOnlyList<View>> ListAsync(CancellationToken ct = default)
    {
        var saved = await _db.Views.AsNoTracking().OrderBy(v => v.Name).ThenBy(v => v.Id).ToListAsync(ct);
        return [View.Overview(), .. saved];
    }

    public async Task<View> GetAsync(Guid id, CancellationToken ct = default) =>
        id == View.OverviewId
            ? View.Overview()
            : await _db.Views.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct)
                ?? throw new EntityNotFoundException(nameof(View), id);

    public async Task<View> CreateAsync(ViewDefinition definition, CancellationToken ct = default)
    {
        var view = View.Create(definition);
        await EnsureValidAsync(view, ct);

        _db.Views.Add(view);
        await SaveAsync(view.Name, ct);
        return view;
    }

    public async Task<View> UpdateAsync(Guid id, ViewDefinition definition, CancellationToken ct = default)
    {
        var view = await FindForEditAsync(id, ct);
        view.Redefine(definition);
        await EnsureValidAsync(view, ct);

        await SaveAsync(view.Name, ct);
        return view;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var view = await FindForEditAsync(id, ct);

        // The foreign key cascades in Postgres; removed here too so InMemory, which has no
        // foreign keys, behaves the same.
        _db.DefaultViews.RemoveRange(await _db.DefaultViews.Where(d => d.ViewId == id).ToListAsync(ct));
        _db.Views.Remove(view);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<View> GetDefaultAsync(CancellationToken ct = default)
    {
        var userId = _caller.Current.AccountId;
        var viewId = await _db.DefaultViews
            .Where(d => d.UserId == userId)
            .Select(d => (Guid?)d.ViewId)
            .FirstOrDefaultAsync(ct);
        return viewId is { } id ? await GetAsync(id, ct) : View.Overview();
    }

    public async Task<View> SetDefaultAsync(Guid viewId, CancellationToken ct = default)
    {
        var view = await GetAsync(viewId, ct);
        var userId = _caller.Current.AccountId;
        var current = await _db.DefaultViews.FirstOrDefaultAsync(d => d.UserId == userId, ct);

        if (view.IsBuiltIn)
        {
            if (current is not null)
            {
                _db.DefaultViews.Remove(current);
            }
        }
        else if (current is null)
        {
            _db.DefaultViews.Add(DefaultView.For(userId, view.Id));
        }
        else
        {
            current.PointTo(view.Id);
        }

        await _db.SaveChangesAsync(ct);
        return view;
    }

    public async Task<WorkItemQueryPage<WorkItemSummary>> RunAsync(Guid id, ViewWindow window, CancellationToken ct = default)
    {
        var view = await GetAsync(id, ct);
        return await _query.QuerySummariesAsync(view.ToQuery(window), ct);
    }

    /// <summary>Overview is checked first, so changing it says why rather than "not found".</summary>
    private async Task<View> FindForEditAsync(Guid id, CancellationToken ct)
    {
        if (id == View.OverviewId)
        {
            throw new BuiltInViewException(View.OverviewName);
        }

        return await _db.Views.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new EntityNotFoundException(nameof(View), id);
    }

    private async Task EnsureValidAsync(View view, CancellationToken ct)
    {
        await EnsureNameFreeAsync(view, ct);
        foreach (var spaceId in view.SpaceIds)
        {
            await _db.EnsureSpaceExistsAsync(spaceId, ct);
        }
    }

    /// <summary>
    /// Names are compared ignoring case, as for Spaces, and Overview's name counts as taken,
    /// since two Views with one name couldn't be told apart in the picker. The unique index only
    /// catches an exact duplicate saved concurrently.
    /// </summary>
    private async Task EnsureNameFreeAsync(View view, CancellationToken ct)
    {
        var lowered = view.Name.ToLower();
        if (lowered == View.OverviewName.ToLower()
            || await _db.Views.AnyAsync(v => v.Id != view.Id && v.Name.ToLower() == lowered, ct))
        {
            throw new ViewNameTakenException(view.Name);
        }
    }

    private async Task SaveAsync(string name, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException ex)
        {
            throw new ViewNameTakenException(name, ex);
        }
    }
}
