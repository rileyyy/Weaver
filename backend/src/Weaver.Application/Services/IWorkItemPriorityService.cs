using Weaver.Domain;

namespace Weaver.Application.Services;

public interface IWorkItemPriorityService : ISpaceLookupService<WorkItemPriority>
{
    /// <summary>Appends a priority to <paramref name="spaceId"/>'s, as its most urgent.</summary>
    Task<WorkItemPriority> CreateAsync(Guid actorUserId, Guid spaceId, string name, CancellationToken ct = default);

    Task<WorkItemPriority> RenameAsync(Guid actorUserId, Guid id, string name, CancellationToken ct = default);
}
