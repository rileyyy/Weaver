using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Application.Persistence;

/// <summary>
/// The work-item query's matching that only the database provider can run: Postgres full-text
/// search, and looking inside the tag array. Kept apart from <see cref="IWeaverDbContext"/> for
/// the same reason as <see cref="IWorkItemHierarchy"/>.
/// </summary>
public interface IWorkItemSearch
{
    /// <summary>
    /// Narrows <paramref name="items"/> to those whose title, description or When contain every
    /// word of <paramref name="text"/>, as a word or the start of one, ignoring case. Text with no
    /// words in it leaves <paramref name="items"/> as it is.
    /// </summary>
    IQueryable<WorkItem> MatchingText(IQueryable<WorkItem> items, string text);

    /// <summary>
    /// Narrows <paramref name="items"/> to those with any (or, for <see cref="TagMatch.All"/>,
    /// every) one of <paramref name="lowerCaseTags"/>, comparing each item's tags lower-cased.
    /// </summary>
    IQueryable<WorkItem> Tagged(IQueryable<WorkItem> items, IReadOnlyList<string> lowerCaseTags, TagMatch match);
}
