using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Weaver.Application.Auth;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Mcp;

/// <summary>
/// Applies <see cref="Caller.EnsureCanWrite"/> to every tool call except tools marked
/// <c>ReadOnly = true</c>. A tool that says nothing counts as a write, so a new tool is
/// covered without opting in, and only an explicit (and reviewable) <c>ReadOnly</c> opts out.
/// </summary>
public static class WriteAccessToolFilter
{
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Apply(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
        async (context, ct) =>
        {
            if (IsReadOnly(context.MatchedPrimitive))
            {
                return await next(context, ct);
            }

            try
            {
                context.Services!.GetRequiredService<ICallerContext>().Current.EnsureCanWrite();
            }
            catch (ReadOnlyApiKeyException ex)
            {
                // A tool error, as when the tool itself fails, so the agent sees the reason.
                return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = ex.Message }] };
            }

            return await next(context, ct);
        };

    private static bool IsReadOnly(IMcpServerPrimitive? primitive) =>
        primitive is McpServerTool { ProtocolTool.Annotations.ReadOnlyHint: true };
}
