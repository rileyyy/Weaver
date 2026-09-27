using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Middleware;

/// <summary>
/// Turns expected failures into <c>application/problem+json</c> responses in one place, so
/// controller actions stay free of try/catch and a new <see cref="DomainException"/> needs no
/// change here. Anything else propagates and becomes a 500.
/// </summary>
/// <remarks>
/// A plain middleware rather than <c>IExceptionHandler</c>: in .NET 9 the exception handler
/// middleware logs every exception as an unhandled error before a handler runs, which would
/// turn each 404 and 409 into error-level log noise.
/// </remarks>
public class ApiExceptionMiddleware
{
    /// <summary>
    /// Services translate concurrency failures they expect; one that still escapes means the
    /// row changed during the request, which the client can resolve by reloading.
    /// </summary>
    public const string ConcurrencyConflictMessage =
        "The data was changed by someone else while saving. Refresh and try again.";

    private readonly RequestDelegate _next;

    public ApiExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetails)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            await WriteProblemAsync(context, problemDetails, DomainErrorStatusCodes.For(ex.Kind), ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            await WriteProblemAsync(context, problemDetails, StatusCodes.Status409Conflict, ConcurrencyConflictMessage);
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        IProblemDetailsService problemDetails,
        int statusCode,
        string detail)
    {
        context.Response.StatusCode = statusCode;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = statusCode, Detail = detail },
        });
    }
}
