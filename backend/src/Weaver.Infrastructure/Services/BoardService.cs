using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Infrastructure.Services;

public class BoardService : IBoardService
{
    private readonly WeaverDbContext _db;

    public BoardService(WeaverDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Board>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Boards.ToListAsync(ct);

    public Task<Board?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Boards.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<Board> CreateAsync(string name, Guid? scopeItemId, CancellationToken ct = default)
    {
        if (scopeItemId is not null && !await _db.WorkItems.AnyAsync(w => w.Id == scopeItemId, ct))
        {
            throw new EntityNotFoundException(nameof(WorkItem), scopeItemId.Value);
        }

        var board = new Board
        {
            Id = Guid.NewGuid(),
            Name = name,
            ScopeItemId = scopeItemId,
        };

        _db.Boards.Add(board);
        await _db.SaveChangesAsync(ct);
        return board;
    }
}
