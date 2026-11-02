using System.Text.Json;
using System.Text.Json.Serialization;
using Weaver.Domain;

namespace Weaver.Api.Json;

/// <summary>See <see cref="OptionalJsonConverterFactory"/>.</summary>
public class OptionalJsonConverter<T> : JsonConverter<Optional<T>>
{
    // Without this, System.Text.Json turns a JSON null into default(Optional<T>) itself,
    // which is Unset, and "clear this field" would read as "leave it alone".
    public override bool HandleNull => true;

    public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(JsonSerializer.Deserialize<T>(ref reader, options)!);

    public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value.GetValueOr(default!), options);
}
