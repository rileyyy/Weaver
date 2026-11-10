using System.Text.Json;

namespace Weaver.Api.IntegrationTests;

internal static class McpJson
{
    /// <summary>Ignores case, since the casing of tool results is the MCP serializer's, not REST's.</summary>
    public static JsonElement? PropertyOf(JsonElement obj, string name) =>
        obj.EnumerateObject()
            .Where(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            .Select(p => (JsonElement?)p.Value)
            .FirstOrDefault();
}
