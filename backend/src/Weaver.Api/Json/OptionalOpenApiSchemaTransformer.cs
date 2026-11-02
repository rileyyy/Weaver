using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Services;
using Weaver.Domain;

namespace Weaver.Api.Json;

/// <summary>
/// Describes <see cref="Optional{T}"/> fields the way <see cref="OptionalJsonConverter{T}"/>
/// puts them on the wire: as the plain value, and never required. Without this the document
/// shows the struct's own shape (<c>hasValue</c>/<c>value</c>), which no client sends.
/// </summary>
/// <remarks>
/// Two passes, because .NET 9 walks into a schema's items and properties after the schema
/// transformer runs, using the type the schema was built from; reshaping an Optional's schema
/// there makes that walk fail. The schema pass only marks Optional schemas, and the document
/// pass, which runs once every schema is built, reshapes them.
/// </remarks>
public class OptionalOpenApiSchemaTransformer : IOpenApiSchemaTransformer, IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (IsOptional(context.JsonTypeInfo.Type))
        {
            schema.Extensions[OptionalSchemaFlattener.OptionalMarker] = new OpenApiBoolean(true);
        }

        foreach (var property in context.JsonTypeInfo.Properties.Where(p => IsOptional(p.PropertyType)))
        {
            schema.Required.Remove(property.Name);
        }

        return Task.CompletedTask;
    }

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        new OpenApiWalker(new OptionalSchemaFlattener()).Walk(document);
        return Task.CompletedTask;
    }

    private static bool IsOptional(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Optional<>);
}
