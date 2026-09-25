using ModelContextProtocol;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Mcp;

/// <summary>
/// Translates the same domain exceptions <see cref="Weaver.Api.Middleware.ApiExceptionMiddleware"/>
/// maps to HTTP status codes into <see cref="McpException"/>, so an MCP client sees the same
/// message a REST caller would (e.g. "Work item {id} was not found.") instead of the generic,
/// detail-free message the SDK substitutes for an unrecognized exception type. This is
/// transport-specific error presentation, not a second copy of any business rule — the
/// exceptions themselves are still only ever thrown from the Application services.
/// </summary>
public static class McpExceptionTranslation
{
    public static async Task<T> TranslateAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (IsKnownDomainException(ex))
        {
            throw new McpException(ex.Message, ex);
        }
    }

    public static async Task TranslateAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception ex) when (IsKnownDomainException(ex))
        {
            throw new McpException(ex.Message, ex);
        }
    }

    private static bool IsKnownDomainException(Exception ex) => ex is
        EntityNotFoundException or
        CyclicParentException or
        WorkItemHasChildrenException or
        WorkItemIsBoardScopeException or
        InvalidWorkItemScheduleException or
        InvalidWorkItemTagException or
        CommentAuthorMismatchException or
        SelfWorkItemLinkException or
        DuplicateWorkItemLinkException;
}
