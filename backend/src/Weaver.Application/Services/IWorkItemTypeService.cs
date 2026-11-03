using Weaver.Domain;

namespace Weaver.Application.Services;

public interface IWorkItemTypeService : ISpaceLookupService<WorkItemType>
{
    /// <summary>Appends a Type after the last of <paramref name="spaceId"/>'s.</summary>
    Task<WorkItemType> CreateAsync(
        Guid actorUserId,
        Guid spaceId,
        string name,
        string color,
        CancellationToken ct = default);

    Task<WorkItemType> UpdateAsync(Guid actorUserId, Guid id, string name, string color, CancellationToken ct = default);
}
