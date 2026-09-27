namespace Weaver.Application.Services;

public interface ICommentService
{
    Task<IReadOnlyList<CommentView>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default);

    Task<CommentView?> GetAsync(Guid id, CancellationToken ct = default);

    Task<CommentView> CreateAsync(Guid workItemId, Guid authorUserId, string body, CancellationToken ct = default);

    /// <summary>Throws <see cref="Weaver.Domain.Exceptions.CommentAuthorMismatchException"/>
    /// if <paramref name="requestingUserId"/> isn't the comment's author.</summary>
    Task<CommentView> UpdateAsync(Guid id, Guid requestingUserId, string body, CancellationToken ct = default);

    /// <summary>Throws <see cref="Weaver.Domain.Exceptions.CommentAuthorMismatchException"/>
    /// if <paramref name="requestingUserId"/> isn't the comment's author.</summary>
    Task DeleteAsync(Guid id, Guid requestingUserId, CancellationToken ct = default);
}
