using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weaver.Application.Services;

/// <summary>
/// How every history value is stored: as JSON in the shape the field has in the API
/// (camelCase, enums by name), so work item and Space history read the same way.
/// </summary>
internal static class HistoryJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string? Serialize(object? value) => value is null ? null : JsonSerializer.Serialize(value, Options);
}
