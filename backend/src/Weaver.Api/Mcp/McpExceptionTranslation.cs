using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using Weaver.Api.Middleware;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Mcp;

/// <summary>
/// Translates the same failures <see cref="ApiExceptionMiddleware"/> maps to HTTP statuses into
/// <see cref="McpException"/>, so an MCP client sees the same message a REST caller would
/// (e.g. "Work item {id} was not found.") instead of the generic, detail-free message the SDK
/// substitutes for an unrecognized exception type. Every <see cref="DomainException"/> is
/// covered, so a new one needs no change here.
/// </summary>
public static class McpExceptionTranslation
{
    public static async Task<T> TranslateAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (DomainException ex)
        {
            throw new McpException(ex.Message, ex);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new McpException(ApiExceptionMiddleware.ConcurrencyConflictMessage, ex);
        }
    }

    public static Task TranslateAsync(Func<Task> operation) =>
        TranslateAsync(async () =>
        {
            await operation();
            return true;
        });
}
