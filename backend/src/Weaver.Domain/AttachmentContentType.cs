using System.Net.Http.Headers;

namespace Weaver.Domain;

public static class AttachmentContentType
{
    /// <summary>
    /// The trimmed media type when it parses as one; null otherwise. It's sent back as the
    /// download's <c>Content-Type</c> header, so anything that isn't a plain media type
    /// (a line break, say) must not get through.
    /// </summary>
    public static string? Sanitize(string? contentType, int maxLength)
    {
        var trimmed = contentType?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength || trimmed.Any(char.IsControl))
        {
            return null;
        }

        return MediaTypeHeaderValue.TryParse(trimmed, out var parsed) && parsed.MediaType?.Contains('/') == true
            ? trimmed
            : null;
    }
}
