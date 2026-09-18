using Weaver.Domain;

namespace Weaver.Infrastructure.Services;

public interface ICommentService
{
    Task<IReadOnlyList<Comment>> ListForWorkItemAsync(Guid workItemId, CancellationToken ct = default);

    Task<Comment> CreateAsync(Guid workItemId, Guid authorUserId, string body, CancellationToken ct = default);

    /// <summary>Throws <see cref="Weaver.Domain.Exceptions.CommentAuthorMismatchException"/>
    /// if <paramref name="requestingUserId"/> isn't the comment's author.</summary>
    Task<Comment> UpdateAsync(Guid id, Guid requestingUserId, string body, CancellationToken ct = default);

    /// <summary>Throws <see cref="Weaver.Domain.Exceptions.CommentAuthorMismatchException"/>
    /// if <paramref name="requestingUserId"/> isn't the comment's author.</summary>
    Task DeleteAsync(Guid id, Guid requestingUserId, CancellationToken ct = default);
}
