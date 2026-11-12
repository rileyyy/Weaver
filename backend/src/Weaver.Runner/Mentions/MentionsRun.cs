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
/// waiting on a bad answer. Only a run that stops (model or Weaver unreachable) leaves mentions
/// pending, for the next run.
/// </summary>
public sealed class MentionsRun(IMentionsWeaverClient weaver, IModelClient model, ILogger<MentionsRun> logger) : ITaskRun
{
    public static readonly string ActorLabel = $"Runner {TaskKind.Mentions.CommandName}";

    private readonly IMentionsWeaverClient _weaver = weaver;
    private readonly IModelClient _model = model;
    private readonly ILogger<MentionsRun> _logger = logger;

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
        var (reply, outcome) = await ComposeAsync(mention, today, ct);
        try
        {
            await _weaver.AddCommentAsync(mention.WorkItemId, reply, ActorLabel, ct);
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

    /// <summary>The reply to post, and how the request went.</summary>
    private async Task<(string Reply, ItemOutcome Outcome)> ComposeAsync(
        WeaverPendingMention mention, DateOnly today, CancellationToken ct)
    {
        WeaverItemDetail item;
        string rawAnswer;
        try
        {
            item = await _weaver.GetItemAsync(mention.WorkItemId, ct);
            var comments = await _weaver.ListCommentsAsync(item.Id, ct);
            rawAnswer = await _model.CompleteAsync(MentionPrompt.Build(mention, item, comments, today), ct);
        }
        catch (Exception e) when (e is WeaverRejectedException or ModelRejectedException)
        {
            var reason = e is WeaverRejectedException
                ? $"Weaver turned down a read for this item ({e.Message})"
                : $"the model turned down the prompt ({e.Message})";
            _logger.LogWarning("#{Number}: failed, {Reason}", mention.WorkItemNumber, reason);
            return (MentionReply.CouldNotAnswer(reason), ItemOutcome.Failed);
        }

        var check = MentionAnswerChecker.Check(rawAnswer, today);
        if (check.Answer is not { } answer)
        {
            _logger.LogWarning("#{Number}: answer rejected, {Reason}. The answer was: {Answer}",
                mention.WorkItemNumber, check.RejectReason, rawAnswer);
            return (MentionReply.CouldNotAnswer($"my answer didn't pass the runner's checks ({check.RejectReason})"), ItemOutcome.Rejected);
        }

        return (await ApplyAsync(item, answer, ct), ItemOutcome.Written);
    }

    /// <summary>
    /// The dates go first because they're written at the version read, and adding a sub-item
    /// could move the item past it. Each addition stands alone: one Weaver refuses is reported in
    /// the reply and the rest still happen.
    /// </summary>
    private async Task<string> ApplyAsync(WeaverItemDetail item, MentionAnswer answer, CancellationToken ct)
    {
        var reply = new MentionReply();
        if (answer.HasDates)
        {
            await SetDatesAsync(item, answer, reply, ct);
        }

        if (answer.SubItems.Count > 0)
        {
            await AddSubItemsAsync(item, answer.SubItems, reply, ct);
        }

        foreach (var link in answer.Links)
        {
            await TryAsync(reply, $"link {Describe(link)}", () => _weaver.AddUrlLinkAsync(item.Id, link.Url, link.Label, ActorLabel, ct));
        }

        return reply.Render(answer.Reply);
    }

    /// <summary>Autonomous agents never change existing values (ADR-0010), so dates on a dated item are only proposed.</summary>
    private Task SetDatesAsync(WeaverItemDetail item, MentionAnswer answer, MentionReply reply, CancellationToken ct)
    {
        var what = $"dates {FormatOrOpen(answer.Start)} to {FormatOrOpen(answer.End)}";
        if (item.HasDates)
        {
            reply.NotDone(what, "Proposed only. The item already has dates, and I don't change existing values; a person can set them");
            return Task.CompletedTask;
        }

        return TryAsync(reply, what, () => _weaver.SetDatesAsync(item.Id, answer.Start, answer.End, item.Version, ActorLabel, ct));
    }

    private async Task AddSubItemsAsync(WeaverItemDetail item, IReadOnlyList<string> titles, MentionReply reply, CancellationToken ct)
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
                var number = await _weaver.AddSubItemAsync(item.Id, title, statusId, ActorLabel, ct);
                reply.Added($"sub-item #{number} {title}");
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

    private static string Describe(MentionLink link) => link.Label is null ? link.Url : $"{link.Label}: {link.Url}";

    private static string FormatOrOpen(DateOnly? date) => date is { } d ? IsoDate.Format(d) : "?";
}
