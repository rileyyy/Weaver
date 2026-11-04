using System.Text.RegularExpressions;
using Weaver.Application.Persistence;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Application.Tests;

/// <summary>
/// <see cref="IWorkItemSearch"/> for the EF InMemory provider, which can neither run Postgres
/// full-text search nor query inside the tag list. Keeps the same contract by matching in C# and
/// narrowing to the matching ids; <c>Weaver.Api.IntegrationTests</c> covers the real one.
/// </summary>
public partial class InMemoryWorkItemSearch : IWorkItemSearch
{
    public IQueryable<WorkItem> MatchingText(IQueryable<WorkItem> items, string text)
    {
        var words = WordsOf(text);
        return words.Count == 0
            ? items
            : Narrow(items, w => words.All(word => WordsOf($"{w.Title} {w.Description} {w.When}").Any(found => found.StartsWith(word, StringComparison.Ordinal))));
    }

    public IQueryable<WorkItem> Tagged(IQueryable<WorkItem> items, IReadOnlyList<string> lowerCaseTags, TagMatch match) =>
        Narrow(items, w =>
        {
            var tags = w.Tags.Select(t => t.ToLowerInvariant()).ToHashSet();
            return match == TagMatch.All ? lowerCaseTags.All(tags.Contains) : lowerCaseTags.Any(tags.Contains);
        });

    private static IQueryable<WorkItem> Narrow(IQueryable<WorkItem> items, Func<WorkItem, bool> matches)
    {
        var matchingIds = items.AsEnumerable().Where(matches).Select(w => w.Id).ToList();
        return items.Where(w => matchingIds.Contains(w.Id));
    }

    private static List<string> WordsOf(string text) =>
        NonWordCharacters().Split(text.ToLowerInvariant()).Where(w => w.Length > 0).ToList();

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NonWordCharacters();
}
