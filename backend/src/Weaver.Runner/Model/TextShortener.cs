namespace Weaver.Runner.Model;

/// <summary>Shortening text the runner sends to a model or posts to Weaver, where each has a length limit.</summary>
public static class TextShortener
{
    public const string Ellipsis = "…";

    /// <summary>
    /// <paramref name="text"/> unchanged if it fits, otherwise cut and ended with an ellipsis so the
    /// result, ellipsis included, is at most <paramref name="max"/> characters: Weaver refuses a
    /// comment over its limit, so the limit has to hold for the whole result.
    /// </summary>
    public static string Shorten(string text, int max)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(max, Ellipsis.Length);
        if (text.Length <= max)
        {
            return text;
        }

        var cut = max - Ellipsis.Length;
        // Cutting between the halves of a surrogate pair (most emoji) would leave invalid UTF-16.
        if (cut > 0 && char.IsHighSurrogate(text[cut - 1]))
        {
            cut--;
        }

        return text[..cut] + Ellipsis;
    }
}
