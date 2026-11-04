using System.Text.RegularExpressions;

namespace Weaver.Infrastructure;

/// <summary>
/// Turns what someone typed into a <c>to_tsquery</c> string that matches every word as a word or
/// the start of one, so a search finds "hallway" while it's still being typed as "hall".
/// </summary>
/// <remarks>
/// Only letters and digits are kept, which leaves nothing for <c>to_tsquery</c>'s own operators
/// (<c>&amp; | ! ( ) : *</c>, quotes) to parse: typed text can't make the query fail.
/// </remarks>
public static partial class PostgresPrefixQuery
{
    /// <summary>Null when the text has no words, which then applies no text filter.</summary>
    public static string? From(string text)
    {
        var words = Words().Matches(text).Select(m => m.Value.ToLowerInvariant() + ":*").Distinct().ToList();
        return words.Count == 0 ? null : string.Join(" & ", words);
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Words();
}
