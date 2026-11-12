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

    /// <summary>
    /// A plain id, or a link's <c>{ "workItemId", "type" }</c>, whose id is the other item.
    /// Link entries from before links had types hold the plain id.
    /// </summary>
    public static Guid? IdIn(string? valueJson)
    {
        if (valueJson is null)
        {
            return null;
        }

        var value = JsonSerializer.Deserialize<JsonElement>(valueJson);
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("workItemId", out var workItemId))
        {
            value = workItemId;
        }

        return value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var id) ? id : null;
    }

    /// <summary>Reads back a value <see cref="Serialize"/> stored; a missing value is the type's default.</summary>
    public static T? Deserialize<T>(string? json) => json is null ? default : JsonSerializer.Deserialize<T>(json, Options);
}
