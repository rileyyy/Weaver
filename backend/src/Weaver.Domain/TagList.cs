namespace Weaver.Domain;

/// <summary>
/// The one rule for a list of tags, wherever it's stored: each trimmed, at most
/// <see cref="WorkItem.TagMaxLength"/> characters, and de-duplicated ignoring case with the first
/// spelling kept, since tags count as one whatever their case.
/// </summary>
public static class TagList
{
    /// <param name="blankTag">The exception to throw for a blank tag.</param>
    public static List<string> Normalize(IEnumerable<string> tags, Func<Exception> blankTag)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            var trimmed = tag.Trim();
            if (trimmed.Length == 0)
            {
                throw blankTag();
            }

            TextValidation.RequireMaxLength(trimmed, "Each tag", WorkItem.TagMaxLength);

            if (seen.Add(trimmed))
            {
                normalized.Add(trimmed);
            }
        }

        return normalized;
    }
}
