using System.Text.Json;
using System.Text.Json.Serialization;
using Weaver.Domain;

namespace Weaver.Api.Json;

/// <summary>
/// Reads <see cref="Optional{T}"/> request fields so patch semantics survive the wire: a
/// property missing from the JSON never reaches the converter and stays
/// <see cref="Optional{T}.Unset"/>, while an explicit <c>null</c> becomes a set value of
/// <c>null</c>.
/// </summary>
public class OptionalJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(OptionalJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;
}
