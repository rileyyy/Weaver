using System.Text.RegularExpressions;
using Weaver.Runner.WeaverApi;

namespace Weaver.Runner.WhenResolution;

/// <summary>
/// Finds the items a When phrase may be waiting for ("after drywall" finds the drywall item), so
/// the model picks from a short list instead of calling search itself (ADR-0010).
/// </summary>
public sealed class BlockerCandidateFinder(IWeaverClient weaver)
{
    public const int MaxCandidates = 5;
    private const int ResultsPerWord = 10;
    private const int MinWordLength = 3;

    /// <summary>
    /// Words that only ever say when, never what. Weaver's search needs every word to match, so
    /// each remaining word is searched on its own and these would match nothing useful.
    /// </summary>
    private static readonly HashSet<string> TimeAndFillerWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "after", "before", "once", "until", "till", "when", "then", "while", "during", "by",
        "the", "and", "for", "with", "from", "into", "this", "that", "these", "those", "our", "its",
        "are", "was", "has", "have", "been", "done", "finished", "finish", "complete", "completed", "ready",
        "next", "last", "early", "late", "end", "start", "mid", "middle",
        "today", "tomorrow", "tonight", "day", "days", "week", "weeks", "weekend", "month", "months", "year", "years",
        "spring", "summer", "autumn", "fall", "winter",
        "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday",
        "january", "february", "march", "april", "may", "june", "july", "august",
        "september", "october", "november", "december",
    };

    private static readonly Regex NonWordCharacters = new(@"[^\p{L}\p{N}]+", RegexOptions.Compiled);

    private readonly IWeaverClient _weaver = weaver;

    public async Task<IReadOnlyList<BlockerCandidate>> FindAsync(WeaverWorkItem item, CancellationToken ct)
    {
        var words = SearchWords(item.When);
        if (words.Count == 0)
        {
            return [];
        }

        var matches = await MatchesByItemAsync(words, ct);
        matches.Remove(item.Id);
        foreach (var linked in await _weaver.ListLinkedItemIdsAsync(item.Id, ct))
        {
            matches.Remove(linked);
        }

        return matches.Values
            .OrderByDescending(m => m.WordCount)
            .ThenByDescending(m => m.Item.SpaceId == item.SpaceId)
            .ThenBy(m => m.Item.Number)
            .Take(MaxCandidates)
            .Select(m => new BlockerCandidate(m.Item.Id, m.Item.Number, m.Item.Title, m.Item.SpaceName, m.Item.ParentTitle))
            .ToList();
    }

    internal static IReadOnlyList<string> SearchWords(string? phrase) =>
        NonWordCharacters.Split(phrase ?? "")
            .Where(w => w.Length >= MinWordLength && !TimeAndFillerWords.Contains(w))
            .Select(w => w.ToLowerInvariant())
            .Distinct()
            .ToList();

    private async Task<Dictionary<Guid, (WeaverWorkItem Item, int WordCount)>> MatchesByItemAsync(
        IReadOnlyList<string> words, CancellationToken ct)
    {
        var matches = new Dictionary<Guid, (WeaverWorkItem Item, int WordCount)>();
        foreach (var word in words)
        {
            foreach (var found in await _weaver.SearchOpenItemsAsync(word, ResultsPerWord, ct))
            {
                var wordCount = matches.TryGetValue(found.Id, out var match) ? match.WordCount : 0;
                matches[found.Id] = (found, wordCount + 1);
            }
        }

        return matches;
    }
}
