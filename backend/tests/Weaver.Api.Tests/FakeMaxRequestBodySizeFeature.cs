using Microsoft.AspNetCore.Http.Features;

namespace Weaver.Api.Tests;

/// <summary>Stands in for Kestrel's per-request body limit, which a bare DefaultHttpContext lacks.</summary>
public sealed class FakeMaxRequestBodySizeFeature : IHttpMaxRequestBodySizeFeature
{
    public bool IsReadOnly => false;

    public long? MaxRequestBodySize { get; set; } = 30_000_000;
}
