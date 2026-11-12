using System.Text.RegularExpressions;

namespace Weaver.Domain;

/// <summary>The <c>@</c> mentions in a comment's text.</summary>
public static partial class Mentions
{
    /// <summary>
    /// Every handle mentioned, lower-cased, each once. An <c>@</c> straight after a letter, digit,
    /// dot or another <c>@</c> isn't a mention, so an email address names nobody.
    /// </summary>
    public static IReadOnlySet<string> Parse(string body) =>
        Mention().Matches(body)
            .Select(m => m.Groups["handle"].Value.ToLowerInvariant())
            .ToHashSet();

    [GeneratedRegex($@"(?<![\w.@])@(?<handle>{Handle.Pattern})", RegexOptions.IgnoreCase)]
    private static partial Regex Mention();
}
