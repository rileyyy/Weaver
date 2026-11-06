namespace Weaver.Api.Mcp;

/// <summary>Content types an agent can read as text, so an attachment of one is sent as text rather than base64.</summary>
public static class TextContentTypes
{
    private static readonly HashSet<string> TextApplicationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/json",
        "application/xml",
        "application/javascript",
        "application/ecmascript",
        "application/x-yaml",
        "application/yaml",
        "application/toml",
        "application/sql",
        "application/x-sh",
        "application/graphql",
    };

    public static bool IsTextLike(string contentType)
    {
        var mediaType = contentType.Split(';', 2)[0].Trim();
        return mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || TextApplicationTypes.Contains(mediaType)
            || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase);
    }
}
