using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Services;

namespace Weaver.Api.Json;

/// <summary>
/// Reshapes every schema <see cref="OptionalOpenApiSchemaTransformer"/> marked as an
/// Optional into the schema of the value it wraps.
/// </summary>
internal class OptionalSchemaFlattener : OpenApiVisitorBase
{
    public const string OptionalMarker = "x-weaver-optional";

    public override void Visit(OpenApiSchema schema)
    {
        if (!schema.Extensions.Remove(OptionalMarker) || !schema.Properties.TryGetValue("value", out var value))
        {
            return;
        }

        schema.Type = value.Type;
        schema.Format = value.Format;
        schema.Items = value.Items;
        schema.Nullable = value.Nullable;
        schema.Reference = value.Reference;
        schema.Properties.Clear();
        schema.Required.Clear();
    }
}
