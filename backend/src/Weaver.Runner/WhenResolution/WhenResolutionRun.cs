using Microsoft.Extensions.Logging;
using Weaver.Runner.Model;
using Weaver.Runner.Tasks;
using Weaver.Runner.WeaverApi;

namespace Weaver.Runner.WhenResolution;

/// <summary>
/// One When resolution run (ADR-0010): the runner lists the unresolved items, asks the model
/// about one at a time, checks each answer and writes only what passed. It only ever sets dates
/// on an item that has none and adds links; it never changes or removes anything.
/// </summary>
public sealed class WhenResolutionRun(IWeaverClient weaver, IModelClient model, ILogger<WhenResolutionRun> logger) : ITaskRun
{
    private readonly IWeaverClient _weaver = weaver;
    private readonly IModelClient _model = model;
    private readonly ILogger<WhenResolutionRun> _logger = logger;
    private readonly BlockerCandidateFinder _candidates = new(weaver);

    public async Task<RunSummary> RunAsync(CancellationToken ct)
    {
        var today = await _weaver.GetTodayAsync(ct);
        var quickPicks = await _weaver.ListQuickPicksAsync(ct);
        var items = await _weaver.ListUnresolvedWhenItemsAsync(ct);
        _logger.LogInformation("{Count} unresolved When items to look at, today is {Today}", items.Count, IsoDate.Format(today));

        var outcomes = new List<ItemOutcome>();
        foreach (var item in items)
        {
            outcomes.Add(await ResolveAsync(item, today, quickPicks, ct));
        }

        return new RunSummary(
            Considered: outcomes.Count,
            Written: outcomes.Count(o => o == ItemOutcome.Written),
            Skipped: outcomes.Count(o => o == ItemOutcome.Skipped),
            Rejected: outcomes.Count(o => o == ItemOutcome.Rejected),
            WriteFailed: outcomes.Count(o => o == ItemOutcome.WriteFailed),
            Failed: outcomes.Count(o => o == ItemOutcome.Failed));
    }

    private async Task<ItemOutcome> ResolveAsync(
        WeaverWorkItem item, DateOnly today, IReadOnlyList<QuickPickRange> quickPicks, CancellationToken ct)
    {
        // Weaver's list already leaves dated items out; checking again here keeps "never
        // overwrite" true even if that rule or the list changes.
        if (item.HasDates)
        {
            _logger.LogInformation("#{Number}: skipped, it already has dates", item.Number);
            return ItemOutcome.Skipped;
        }

        if (await AskAsync(item, today, quickPicks, ct) is not var (candidates, reply))
        {
            return ItemOutcome.Failed;
        }

        var check = WhenAnswerChecker.Check(reply, today, candidates);

        switch (check.Outcome)
        {
            case WhenAnswerOutcome.Skip:
                _logger.LogInformation("#{Number} \"{When}\": the model skipped it ({Reason})", item.Number, item.When, check.Reason);
                return ItemOutcome.Skipped;
            case WhenAnswerOutcome.Reject:
                _logger.LogWarning("#{Number} \"{When}\": answer rejected, {Reason}. The answer was: {Reply}",
                    item.Number, item.When, check.Reason, reply);
                return ItemOutcome.Rejected;
            default:
                return await WriteAsync(item, check.Answer!, ct);
        }
    }

    /// <summary>The candidates offered and the model's reply, or null if Weaver or the model turned down a request for this item.</summary>
    private async Task<(IReadOnlyList<BlockerCandidate> Candidates, string Reply)?> AskAsync(
        WeaverWorkItem item, DateOnly today, IReadOnlyList<QuickPickRange> quickPicks, CancellationToken ct)
    {
        try
        {
            var candidates = await _candidates.FindAsync(item, ct);
            var reply = await _model.CompleteAsync(WhenPrompt.Build(item, today, quickPicks, candidates), ct);
            return (candidates, reply);
        }
        catch (Exception e) when (e is WeaverRejectedException or ModelRejectedException)
        {
            _logger.LogWarning("#{Number} \"{When}\": failed, {Reason}", item.Number, item.When, e.Message);
            return null;
        }
    }

    /// <summary>
    /// The dates go first because they're written at the version read: if someone changed the
    /// item since, Weaver refuses them and the link is never added, so nothing is half-written.
    /// </summary>
    private async Task<ItemOutcome> WriteAsync(WeaverWorkItem item, WhenAnswer answer, CancellationToken ct)
    {
        var actorLabel = $"Runner {TaskKind.WhenResolution.CommandName}, confidence {answer.Confidence.ToString().ToLowerInvariant()}";
        try
        {
            if (answer.HasDates)
            {
                await _weaver.SetDatesAsync(item.Id, answer.Start, answer.End, item.Version, actorLabel, ct);
            }

            if (answer.BlockedBy is { } blocker)
            {
                await _weaver.AddBlockedByLinkAsync(item.Id, blocker.Id, actorLabel, ct);
            }
        }
        catch (WeaverRejectedException e)
        {
            _logger.LogWarning("#{Number} \"{When}\": Weaver refused the write, {Reason}", item.Number, item.When, e.Message);
            return ItemOutcome.WriteFailed;
        }

        _logger.LogInformation("#{Number} \"{When}\": resolved to {Resolution} ({Confidence} confidence)",
            item.Number, item.When, Describe(answer), answer.Confidence);
        return ItemOutcome.Written;
    }

    private static string Describe(WhenAnswer answer)
    {
        var parts = new List<string>();
        if (answer.HasDates)
        {
            parts.Add($"{FormatOrUnknown(answer.Start)} to {FormatOrUnknown(answer.End)}");
        }

        if (answer.BlockedBy is { } blocker)
        {
            parts.Add($"blocked by #{blocker.Number} {blocker.Title}");
        }

        return string.Join(", ", parts);
    }

    private static string FormatOrUnknown(DateOnly? date) => date is { } d ? IsoDate.Format(d) : "?";
}
