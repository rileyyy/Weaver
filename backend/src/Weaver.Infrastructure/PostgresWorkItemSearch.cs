using Microsoft.EntityFrameworkCore;
using Weaver.Application.Persistence;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Infrastructure;

/// <summary>Postgres full-text search over title, description and When, and tag matching inside the array.</summary>
public class PostgresWorkItemSearch : IWorkItemSearch
{
    public const string IndexName = "IX_WorkItems_TextSearch";
    public const string TextSearchConfig = "simple";

    public IQueryable<WorkItem> MatchingText(IQueryable<WorkItem> items, string text)
    {
        var prefixQuery = PostgresPrefixQuery.From(text);
        if (prefixQuery is null)
        {
            return items;
        }

        // Written to translate to the exact expression of the GIN index in WorkItemConfiguration
        // (Npgsql's tsvector expression index), so Postgres can use it.
        return items.Where(w =>
            EF.Functions.ToTsVector(TextSearchConfig, w.Title + " " + (w.Description ?? "") + " " + (w.When ?? ""))
                .Matches(EF.Functions.ToTsQuery(TextSearchConfig, prefixQuery)));
    }

    public IQueryable<WorkItem> Tagged(IQueryable<WorkItem> items, IReadOnlyList<string> lowerCaseTags, TagMatch match)
    {
        if (match == TagMatch.Any)
        {
            return items.Where(w => w.Tags.Any(t => lowerCaseTags.Contains(t.ToLower())));
        }

        foreach (var tag in lowerCaseTags)
        {
            items = items.Where(w => w.Tags.Any(t => t.ToLower() == tag));
        }

        return items;
    }
}
