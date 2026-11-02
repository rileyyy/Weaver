using Microsoft.AspNetCore.Mvc.Filters;
using Weaver.Application.Auth;

namespace Weaver.Api.Auth;

/// <summary>
/// Applies <see cref="Caller.EnsureCanWrite"/> to every REST request that isn't a GET, HEAD or
/// OPTIONS. It's registered globally, so a new controller action is covered without opting in.
/// It runs before model binding, so a read-only key gets the same 403 whatever it sends.
/// </summary>
public sealed class WriteAccessFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        if (IsRead(http.Request.Method) || http.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        http.RequestServices.GetRequiredService<ICallerContext>().Current.EnsureCanWrite();
    }

    private static bool IsRead(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);
}
