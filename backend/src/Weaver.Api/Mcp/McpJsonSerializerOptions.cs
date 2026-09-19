using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weaver.Api.Mcp;

/// <summary>
/// MCP tool parameters/results follow the same "enums serialize as their string name,
/// not the underlying int" contract as the REST API's own JSON (see the
/// <see cref="JsonStringEnumConverter"/> registered for MVC in Program.cs) — an MCP
/// client sees e.g. "Medium" for <see cref="Weaver.Domain.WorkItemPriority"/>, not 1.
/// </summary>
/// <remarks>
/// Must be built from <see cref="JsonSerializerOptions.Default"/> (which already carries a
/// reflection-based <see cref="JsonSerializerOptions.TypeInfoResolver"/>), not a bare
/// <c>new JsonSerializerOptions(...)</c> — the MCP SDK marks whatever options it's given as
/// read-only via <c>MakeReadOnly()</c> before use, which throws if no resolver is set. Caught
/// by an end-to-end run against the dev stack: the API failed to even start, with
/// "JsonSerializerOptions instance must specify a TypeInfoResolver setting before being
/// marked as read-only" thrown from deep inside MapMcp's tool discovery.
/// </remarks>
internal static class McpJsonSerializerOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerOptions.Default)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
