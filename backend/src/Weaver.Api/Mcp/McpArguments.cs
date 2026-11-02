using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Weaver.Domain;

namespace Weaver.Api.Mcp;

/// <summary>
/// Patch semantics for MCP tools. The SDK binds a left-out argument and an explicit
/// <c>null</c> to the same parameter value, so whether the agent sent the argument at all
/// is read from the raw call instead. The tool keeps its typed parameter, and with it an
/// accurate input schema.
/// </summary>
public static class McpArguments
{
    public static Optional<T> Supplied<T>(this RequestContext<CallToolRequestParams> context, string name, T value) =>
        context.Params?.Arguments?.ContainsKey(name) == true ? value : Optional<T>.Unset;
}
