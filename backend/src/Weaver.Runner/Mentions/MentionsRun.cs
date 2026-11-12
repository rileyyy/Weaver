using Microsoft.Extensions.Logging;
using Weaver.Runner.Model;
using Weaver.Runner.Tasks;
using Weaver.Runner.WeaverApi;

namespace Weaver.Runner.Mentions;

/// <summary>
/// One run answering this agent's pending <c>@</c> mentions (ticket 51, ADR-0010). For each, the
/// runner shows the model the item and its thread, checks the answer, makes only additions
/// (sub-items, URL links, dates on an undated item) and then always replies on the item, even to
/// say it couldn't help: the reply is what clears the mention, so nobody's request is left
/// waiting on a bad answer.
/// </summary>
/// <remarks>
/// A mention stays pending when its reply isn't posted (Weaver refused it, or the run stopped
/// because the model or Weaver was unreachable), so a later run answers it again. The additions
/// are safe to repeat: every write for a mention carries the mention's own run id
/// (<see cref="RunIdFor"/>), so the next attempt reads what earlier ones added
/// (<see cref="EarlierAttempt"/>), doesn't add that kind of thing again, and lists it in its reply.
/// </remarks>
public sealed class MentionsRun(IMentionsWeaverClient weaver, IModelClient model, ILogger<MentionsRun> logger) : ITaskRun
{
    public static readonly string ActorLabel = $"Runner {TaskKind.Mentions.CommandName}";

    private readonly IMentionsWeaverClient _weaver = weaver;
    private readonly IModelClient _model = model;
    private readonly ILogger<MentionsRun> _logger = logger;
    private readonly EarlierAttemptReader _earlierAttempts = new(weaver);

    /// <summary>
    /// The run id for every write answering <paramref name="mention"/>, in whichever run. One per
    /// mention rather than per scheduled run, so a retry's writes join the first attempt's and the
    /// run reports show each answered mention as one run.
    /// </summary>
    public static string RunIdFor(WeaverPendingMention mention) => $"mention {mention.Comment.Id}";

    public async Task<RunSummary> RunAsync(CancellationToken ct)
    {
        var today = await _weaver.GetTodayAsync(ct);
        var pending = await _weaver.ListPendingMentionsAsync(ct);
        _logger.LogInformation("{Count} pending mentions to answer", pending.Count);

        var outcomes = new List<ItemOutcome>();
        foreach (var mention in pending)
        {
            outcomes.Add(await AnswerAsync(mention, today, ct));
        }

        return new RunSummary(
            Considered: outcomes.Count,
            Written: outcomes.Count(o => o == ItemOutcome.Written),
            Skipped: outcomes.Count(o => o == ItemOutcome.Skipped),
            Rejected: outcomes.Count(o => o == ItemOutcome.Rejected),
            WriteFailed: outcomes.Count(o => o == ItemOutcome.WriteFailed),
            Failed: outcomes.Count(o => o == ItemOutcome.Failed));
    }

    private async Task<ItemOutcome> AnswerAsync(WeaverPendingMention mention, DateOnly today, CancellationToken ct)
    {
        var runId = RunIdFor(mention);
        var (reply, outcome) = await ComposeAsync(mention, runId, today, ct);
        try
        {
            await _weaver.AddCommentAsync(mention.WorkItemId, reply, ActorLabel, runId, ct);
        }
        catch (WeaverRejectedException e)
        {
            _logger.LogWarning("#{Number}: Weaver refused the reply to {Author}'s mention, {Reason}",
                mention.WorkItemNumber, mention.Comment.AuthorUsername, e.Message);
            return ItemOutcome.WriteFailed;
        }

        _logger.LogInformation("#{Number}: replied to {Author}'s mention ({Outcome})",
            mention.WorkItemNumber, mention.Comment.AuthorUsername, outcome);
        return outcome;
    }

    /// <summary>
    /// The reply to post, and how the request went. What earlier attempts added is read before the
    /// model is asked, so even a retry that can't answer tells the person what's already there.
    /// </summary>
    private async Task<(string Reply, ItemOutcome Outcome)> ComposeAsync(
        WeaverPendingMention mention, string runId, DateOnly today, CancellationToken ct)
    {
        var reply = new MentionReply();
        WeaverItemDetail item;
        EarlierAttempt earlier;
        string rawAnswer;
        try
        {
            item = await _weaver.GetItemAsync(mention.WorkItemId, ct);
            earlier = await _earlierAttempts.ReadAsync(mention, runId, ct);
            ReportEarlierAdditions(earlier, reply);
            var comments = await _weaver.ListCommentsAsync(item.Id, ct);
            rawAnswer = await _model.CompleteAsync(MentionPrompt.Build(mention, item, comments, today), ct);
        }
        catch (Exception e) when (e is WeaverRejectedException or ModelRejectedException)
        {
            var reason = e is WeaverRejectedException
                ? $"Weaver turned down a read for this item ({e.Message})"
                : $"the model turned down the prompt ({e.Message})";
            _logger.LogWarning("#{Number}: failed, {Reason}", mention.WorkItemNumber, reason);
            return (reply.RenderCouldNotAnswer(reason), ItemOutcome.Failed);
        }

        var check = MentionAnswerChecker.Check(rawAnswer, today);
        if (check.Answer is not { } answer)
        {
            _logger.LogWarning("#{Number}: answer rejected, {Reason}. The answer was: {Answer}",
                mention.WorkItemNumber, check.RejectReason, rawAnswer);
            var reason = $"my answer didn't pass the runner's checks ({check.RejectReason})";
            return (reply.RenderCouldNotAnswer(reason), ItemOutcome.Rejected);
        }

        await ApplyAsync(item, answer, earlier, runId, reply, ct);
        return (reply.Render(answer.Reply), ItemOutcome.Written);
    }

    private static void ReportEarlierAdditions(EarlierAttempt earlier, MentionReply reply)
    {
        if (earlier.Dates is { } dates)
        {
            reply.Added(DescribeDates(dates.Start, dates.End));
        }

        foreach (var subItem in earlier.SubItems)
        {
            reply.Added(DescribeSubItem(subItem.Number, subItem.Title));
        }

        foreach (var link in earlier.Links)
        {
            reply.Added(DescribeLink(link));
        }
    }

    /// <summary>
    /// The dates go first because they're written at the version read, and adding a sub-item
    /// could move the item past it. Each addition stands alone: one Weaver refuses is reported in
    /// the reply and the rest still happen. A kind of addition an earlier attempt at this mention
    /// already made is skipped whole rather than matched one by one: the model may word a retry's
    /// sub-items differently, and they would be duplicates all the same.
    /// </summary>
    private async Task ApplyAsync(
        WeaverItemDetail item,
        MentionAnswer answer,
        EarlierAttempt earlier,
        string runId,
        MentionReply reply,
        CancellationToken ct)
    {
        if (answer.HasDates && earlier.Dates is null)
        {
            await SetDatesAsync(item, answer, runId, reply, ct);
        }

        if (answer.SubItems.Count > 0 && earlier.SubItems.Count == 0)
        {
            await AddSubItemsAsync(item, answer.SubItems, runId, reply, ct);
        }

        if (earlier.Links.Count > 0)
        {
            return;
        }

        foreach (var link in answer.Links)
        {
            await TryAsync(reply, DescribeLink(link),
                () => _weaver.AddUrlLinkAsync(item.Id, link.Url, link.Label, ActorLabel, runId, ct));
        }
    }

    /// <summary>Autonomous agents never change existing values (ADR-0010), so dates on a dated item are only proposed.</summary>
    private Task SetDatesAsync(WeaverItemDetail item, MentionAnswer answer, string runId, MentionReply reply, CancellationToken ct)
    {
        var what = DescribeDates(answer.Start, answer.End);
        if (item.HasDates)
        {
            reply.NotDone(what, "Proposed only. The item already has dates, and I don't change existing values; a person can set them");
            return Task.CompletedTask;
        }

        return TryAsync(reply, what,
            () => _weaver.SetDatesAsync(item.Id, answer.Start, answer.End, item.Version, ActorLabel, runId, ct));
    }

    private async Task AddSubItemsAsync(
        WeaverItemDetail item, IReadOnlyList<string> titles, string runId, MentionReply reply, CancellationToken ct)
    {
        Guid statusId;
        try
        {
            statusId = await _weaver.GetFirstToDoStatusIdAsync(item.SpaceId, ct);
        }
        catch (WeaverRejectedException e)
        {
            foreach (var title in titles)
            {
                reply.NotDone($"sub-item \"{title}\"", $"Weaver wouldn't list the Space's statuses ({e.Message})");
            }

            return;
        }

        foreach (var title in titles)
        {
            try
            {
                var number = await _weaver.AddSubItemAsync(item.Id, title, statusId, ActorLabel, runId, ct);
                reply.Added(DescribeSubItem(number, title));
            }
            catch (WeaverRejectedException e)
            {
                reply.NotDone($"sub-item \"{title}\"", $"Weaver refused it ({e.Message})");
            }
        }
    }

    private static async Task TryAsync(MentionReply reply, string what, Func<Task> write)
    {
        try
        {
            await write();
            reply.Added(what);
        }
        catch (WeaverRejectedException e)
        {
            reply.NotDone(what, $"Weaver refused it ({e.Message})");
        }
    }

    private static string DescribeDates(DateOnly? start, DateOnly? end) =>
        $"dates {IsoDate.FormatOrOpen(start)} to {IsoDate.FormatOrOpen(end)}";

    private static string DescribeSubItem(int number, string title) => $"sub-item #{number} {title}";

    private static string DescribeLink(MentionLink link) =>
        link.Label is null ? $"link {link.Url}" : $"link {link.Label}: {link.Url}";
}
