namespace Weaver.Domain;

public static class AttachmentFileName
{
    // Kept whole when a long name is cut, so the file still opens with the right program.
    private const int MaxKeptExtensionLength = 20;

    /// <summary>
    /// The last path segment of <paramref name="fileName"/>, without control characters,
    /// trimmed and cut to <paramref name="maxLength"/>; null when nothing usable is left.
    /// </summary>
    /// <remarks>
    /// Both separators are split on, not just the server's: <see cref="Path.GetFileName(string)"/>
    /// on Linux leaves a Windows client's <c>C:\Users\me\quote.pdf</c> whole.
    /// </remarks>
    public static string? Sanitize(string? fileName, int maxLength)
    {
        if (fileName is null)
        {
            return null;
        }

        var lastSegment = fileName[(fileName.LastIndexOfAny(['/', '\\']) + 1)..];
        var name = new string(lastSegment.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (name.Length == 0)
        {
            return null;
        }

        return name.Length <= maxLength ? name : Shorten(name, maxLength);
    }

    private static string Shorten(string name, int maxLength)
    {
        var extension = Path.GetExtension(name);
        if (extension.Length > MaxKeptExtensionLength)
        {
            extension = string.Empty;
        }

        var stemLength = maxLength - extension.Length;
        if (char.IsHighSurrogate(name[stemLength - 1]))
        {
            stemLength--;
        }

        return name[..stemLength] + extension;
    }
}
