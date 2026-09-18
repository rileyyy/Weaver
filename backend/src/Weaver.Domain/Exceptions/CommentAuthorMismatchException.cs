namespace Weaver.Domain.Exceptions;

/// <summary>
/// Thrown when a user tries to edit or delete a comment they didn't author.
/// There's no roles/admin system yet to allow anyone else to moderate.
/// </summary>
public class CommentAuthorMismatchException : Exception
{
    public CommentAuthorMismatchException(Guid commentId)
        : base($"Comment {commentId} can only be edited or deleted by its author.")
    {
    }
}
