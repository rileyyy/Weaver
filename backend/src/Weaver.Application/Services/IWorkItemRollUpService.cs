using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>Roll-ups of an item's descendants' number and currency fields (ADR-0002), computed on read.</summary>
public interface IWorkItemRollUpService
{
    /// <summary>
    /// A total for each rolled-up field that one of the item's live descendants has a value for:
    /// the fields of the item's own Type first, then other Types' fields in Type order. The item's own value
    /// isn't counted, and trashed items are left out.
    /// </summary>
    /// <exception cref="Weaver.Domain.Exceptions.EntityNotFoundException">The item doesn't exist or is in the Trash.</exception>
    Task<IReadOnlyList<RollUpTotal>> GetAsync(Guid itemId, CancellationToken ct = default);
}
