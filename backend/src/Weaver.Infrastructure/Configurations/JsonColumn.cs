using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Weaver.Infrastructure.Configurations;

/// <summary>
/// Stores a property as one <c>jsonb</c> document, for values whose shape is defined at runtime
/// (custom fields, ADR-0002) and so can't have columns of their own.
/// </summary>
internal static class JsonColumn
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> property) where T : class
    {
        // Compared by their JSON, so change tracking sees an edit to a value inside the
        // document, which reference equality would miss.
        var comparer = new ValueComparer<T>(
            (a, b) => Serialize(a) == Serialize(b),
            v => Serialize(v).GetHashCode(),
            v => Deserialize<T>(Serialize(v)));

        property
            .HasConversion(v => Serialize(v), v => Deserialize<T>(v), comparer)
            .HasColumnType("jsonb")
            .IsRequired();
        return property;
    }

    private static string Serialize<T>(T? value) => JsonSerializer.Serialize(value, Options);

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("A JSON column held null.");
}
