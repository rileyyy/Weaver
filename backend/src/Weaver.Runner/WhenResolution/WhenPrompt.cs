using System.Globalization;
using System.Text;
using Weaver.Runner.Model;
using Weaver.Runner.WeaverApi;

namespace Weaver.Runner.WhenResolution;

/// <summary>
/// The one question asked about each unresolved item (ticket 46, "Small models"): the item and
/// its phrase, today and the quick picks in the server's time zone, and the candidate blockers.
/// The model answers in strict JSON and never calls tools.
/// </summary>
public static class WhenPrompt
{
    private const string Instructions =
        """
        You resolve the "When" phrase of one work item into dates, into the item it waits for, or both.
        Reply with exactly one JSON object and nothing else: no prose, no code fence.

        To resolve it:
        {"start":"YYYY-MM-DD" or null,"end":"YYYY-MM-DD" or null,"blockedBy":<candidate number> or null,"confidence":"low"|"medium"|"high"}

        To leave it for a human:
        {"skip":"<short reason>"}

        Rules:
        - Dates are on or after today, and end is not before start.
        - When the phrase means one of the quick picks, use that quick pick's dates.
        - Set blockedBy only when the phrase names other work that must finish first and exactly one
          candidate clearly is that work. Use the candidate's number.
        - If the phrase is ambiguous, or could mean several dates or items, skip it.
        """;

    public static ModelPrompt Build(
        WeaverWorkItem item,
        DateOnly today,
        IReadOnlyList<QuickPickRange> quickPicks,
        IReadOnlyList<BlockerCandidate> candidates)
    {
        var user = new StringBuilder();
        user.AppendLine(CultureInfo.InvariantCulture, $"Today: {IsoDate.Format(today)} ({today.DayOfWeek})");
        user.AppendLine("Quick picks:");
        foreach (var pick in quickPicks)
        {
            user.AppendLine(CultureInfo.InvariantCulture, $"- {pick.Pick}: {IsoDate.Format(pick.StartDate)} to {IsoDate.Format(pick.EndDate)}");
        }

        user.AppendLine();
        user.AppendLine(CultureInfo.InvariantCulture, $"Work item: #{item.Number} {item.Title}");
        user.AppendLine(CultureInfo.InvariantCulture, $"When: {item.When}");
        user.AppendLine(CultureInfo.InvariantCulture, $"Space: {item.SpaceName}");
        if (item.ParentTitle is not null)
        {
            user.AppendLine(CultureInfo.InvariantCulture, $"Parent: #{item.ParentNumber} {item.ParentTitle}");
        }

        user.AppendLine();
        AppendCandidates(user, candidates);
        return new ModelPrompt(Instructions, user.ToString());
    }

    private static void AppendCandidates(StringBuilder user, IReadOnlyList<BlockerCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            user.AppendLine("Candidates it could wait for: none");
            return;
        }

        user.AppendLine("Candidates it could wait for:");
        foreach (var candidate in candidates)
        {
            var parent = candidate.ParentTitle is null ? "" : $", parent: {candidate.ParentTitle}";
            user.AppendLine(CultureInfo.InvariantCulture, $"- #{candidate.Number} {candidate.Title} (Space: {candidate.SpaceName}{parent})");
        }
    }
}
