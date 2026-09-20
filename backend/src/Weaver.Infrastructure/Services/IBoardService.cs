using Weaver.Domain;

namespace Weaver.Infrastructure.Services;

public interface IBoardService
{
    Task<IReadOnlyList<Board>> GetAllAsync(CancellationToken ct = default);

    Task<Board?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Throws <see cref="Weaver.Domain.Exceptions.EntityNotFoundException"/> if
    /// <paramref name="scopeItemId"/> is set but doesn't match an existing work item.
    /// </summary>
    Task<Board> CreateAsync(string name, Guid? scopeItemId, CancellationToken ct = default);
}
