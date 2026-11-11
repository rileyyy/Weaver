using System.Globalization;
using System.Text;
using Weaver.Runner.Model;
using Weaver.Runner.WeaverApi;
using Weaver.Runner.WhenResolution;

namespace Weaver.Runner.Mentions;

/// <summary>
/// The one question asked about each pending mention: the item, the comment thread up to the
/// request, and today in the server's time zone. The model answers in strict JSON and never calls
/// tools; the runner makes every write (ADR-0010).
/// </summary>
public static class MentionPrompt
{
    /// <summary>Keeps a long thread inside a small model's context window; the newest comments matter most.</summary>
    public const int MaxThreadComments = 20;

    private const int MaxCommentLength = 1000;
    private const int MaxDescriptionLength = 2000;

    private const string Instructions =
        """
        You are an assistant in Weaver, a work tracker. Someone mentioned you in a comment on a work item
        and asked you for something. Answer that request.
        Reply with exactly one JSON object and nothing else: no prose, no code fence.

        {"reply":"<your answer, posted as your comment on the item>",
         "subItems":["<title of a sub-item to add>"],
         "links":[{"url":"https://...","label":"<short label>"}],
         "start":"YYYY-MM-DD" or null,"end":"YYYY-MM-DD" or null}

        Only "reply" is required. Leave out anything you aren't adding.

        Rules:
        - You may only add: sub-items, links to web pages, and dates on an item that has none.
          You never change or remove anything.
        - If the request needs an existing value changed (a title, a status, dates the item already
          has), don't change it: say in your reply exactly what you'd change, so a person can do it.
        - If you can't do what's asked, say so in your reply and why.
        - Dates are on or after today, and end is not before start.
        - Only give links you know exist. Never make up a URL.
        - Keep the reply short and plain.
        """;

    public static ModelPrompt Build(
        WeaverPendingMention mention,
        WeaverItemDetail item,
        IReadOnlyList<WeaverComment> comments,
        DateOnly today)
    {
        var user = new StringBuilder();
        user.AppendLine(CultureInfo.InvariantCulture, $"Today: {IsoDate.Format(today)} ({today.DayOfWeek})");
        user.AppendLine();
        AppendItem(user, item);
        user.AppendLine();
        AppendThread(user, mention.Comment, comments);
        user.AppendLine();
        user.AppendLine(CultureInfo.InvariantCulture, $"The request to answer is this comment from {mention.Comment.AuthorUsername}:");
        user.AppendLine(Shorten(mention.Comment.Body, MaxCommentLength));
        return new ModelPrompt(Instructions, user.ToString());
    }

    private static void AppendItem(StringBuilder user, WeaverItemDetail item)
    {
        user.AppendLine(CultureInfo.InvariantCulture, $"Work item: #{item.Number} {item.Title}");
        user.AppendLine(CultureInfo.InvariantCulture, $"Space: {item.SpaceName}");
        user.AppendLine(CultureInfo.InvariantCulture, $"Status: {item.StatusName}");
        user.AppendLine(CultureInfo.InvariantCulture, $"Dates: {DescribeDates(item)}");
        if (item.When is not null)
        {
            user.AppendLine(CultureInfo.InvariantCulture, $"When: {item.When}");
        }

        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            user.AppendLine("Description:");
            user.AppendLine(Shorten(item.Description, MaxDescriptionLength));
        }
    }

    /// <summary>Comments after the request are left out: they weren't there when it was asked.</summary>
    private static void AppendThread(StringBuilder user, WeaverComment request, IReadOnlyList<WeaverComment> comments)
    {
        var thread = comments
            .Where(c => c.CreatedAtUtc <= request.CreatedAtUtc)
            .TakeLast(MaxThreadComments)
            .ToList();
        user.AppendLine("Comments, oldest first:");
        foreach (var comment in thread)
        {
            user.AppendLine(CultureInfo.InvariantCulture, $"- {comment.AuthorUsername}: {Shorten(comment.Body, MaxCommentLength)}");
        }
    }

    private static string DescribeDates(WeaverItemDetail item) =>
        item.HasDates
            ? $"{FormatOrOpen(item.StartDate)} to {FormatOrOpen(item.EndDate)}"
            : "none";

    private static string FormatOrOpen(DateOnly? date) => date is { } d ? IsoDate.Format(d) : "?";

    private static string Shorten(string text, int max) => text.Length <= max ? text : text[..max] + "…";
}
