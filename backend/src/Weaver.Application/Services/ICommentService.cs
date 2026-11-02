namespace Weaver.Application.Services;

public interface ICommentService
{
    Task<IReadOnlyList<CommentView>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default);

    Task<CommentView?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Adds a comment authored by the caller.</summary>
    Task<CommentView> CreateAsync(Guid workItemId, string body, CancellationToken ct = default);

    /// <summary>Throws <see cref="Weaver.Domain.Exceptions.CommentAuthorMismatchException"/>
    /// if the caller isn't the comment's author.</summary>
    Task<CommentView> UpdateAsync(Guid id, string body, CancellationToken ct = default);

    /// <summary>Throws <see cref="Weaver.Domain.Exceptions.CommentAuthorMismatchException"/>
    /// if the caller isn't the comment's author.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
