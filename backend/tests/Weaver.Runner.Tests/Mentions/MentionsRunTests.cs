using Microsoft.Extensions.Logging.Abstractions;
using Weaver.Runner.Mentions;
using Weaver.Runner.Model;
using Weaver.Runner.Tests.Fakes;
using Weaver.Runner.WeaverApi;

namespace Weaver.Runner.Tests.Mentions;

[TestFixture]
public class MentionsRunTests
{
    private FakeMentionsWeaverClient _weaver = null!;
    private FakeModelClient _model = null!;
    private MentionsRun _run = null!;

    [SetUp]
    public void SetUp()
    {
        _weaver = new FakeMentionsWeaverClient();
        _model = new FakeModelClient();
        _run = new MentionsRun(_weaver, _model, NullLogger<MentionsRun>.Instance);
    }

    [Test]
    public async Task APendingMention_GetsTheModelsReplyAsACommentOnItsItem()
    {
        var item = _weaver.AddItem("Kitchen quotes");
        _weaver.Mention(item, "@claude-research what should I ask the fitters?");
        _model.Answer = _ => """{"reply":"Ask about lead times and the warranty."}""";

        var summary = await _run.RunAsync(CancellationToken.None);

        var reply = _weaver.Replies.Single();
        Assert.Multiple(() =>
        {
            Assert.That(reply.WorkItemId, Is.EqualTo(item.Id));
            Assert.That(reply.Body, Is.EqualTo("Ask about lead times and the warranty."));
            Assert.That(reply.ActorLabel, Is.EqualTo("Runner mentions"));
            Assert.That(_weaver.Pending, Is.Empty);
            Assert.That(summary.Considered, Is.EqualTo(1));
            Assert.That(summary.Written, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task TheModelIsShownTheItemTheThreadAndTheRequest()
    {
        var item = _weaver.AddItem("Kitchen quotes", description: "Three fitters in town");
        _weaver.AddComment(item.Id, FakeMentionsWeaverClient.Riley, "riley", "Budget is 8k");
        _weaver.Mention(item, "@claude-research find three quotes");
        _model.Answer = _ => """{"reply":"On it."}""";

        await _run.RunAsync(CancellationToken.None);

        var prompt = _model.Prompts.Single();
        Assert.Multiple(() =>
        {
            Assert.That(prompt.User, Does.Contain("#1 Kitchen quotes").And.Contain("Three fitters in town"));
            Assert.That(prompt.User, Does.Contain("riley: Budget is 8k"));
            Assert.That(prompt.User, Does.Contain("riley: @claude-research find three quotes"));
            Assert.That(prompt.System, Does.Contain("never change or remove"));
        });
    }

    [Test]
    public async Task SubItemsLinksAndDatesOnAnUndatedItem_AreAdded_AndTheReplySaysSo()
    {
        var item = _weaver.AddItem("Kitchen quotes");
        _weaver.Mention(item, "@claude-research plan this for next week");
        _model.Answer = _ =>
            """
            {"reply":"Here's a plan.",
             "subItems":["Call Fitters Ltd","Call Kitchens R Us"],
             "links":[{"url":"https://fitters.example/quote","label":"Fitters Ltd"}],
             "start":"2026-11-16","end":"2026-11-20"}
            """;

        await _run.RunAsync(CancellationToken.None);

        var reply = _weaver.Replies.Single().Body;
        Assert.Multiple(() =>
        {
            Assert.That(_weaver.SubItems.Select(s => (s.ParentId, s.Title, s.StatusId)), Is.EqualTo(new[]
            {
                (item.Id, "Call Fitters Ltd", FakeMentionsWeaverClient.ToDo),
                (item.Id, "Call Kitchens R Us", FakeMentionsWeaverClient.ToDo),
            }));
            Assert.That(_weaver.UrlLinks.Single().Url, Is.EqualTo("https://fitters.example/quote"));
            Assert.That(_weaver.DateWrites.Single(), Is.EqualTo(new DateWrite(
                item.Id, new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 20), item.Version, "Runner mentions")));
            Assert.That(reply, Does.StartWith("Here's a plan."));
            Assert.That(reply, Does.Contain("#101 Call Fitters Ltd").And.Contain("#102 Call Kitchens R Us"));
            Assert.That(reply, Does.Contain("https://fitters.example/quote"));
            Assert.That(reply, Does.Contain("2026-11-16 to 2026-11-20"));
        });
    }

    [Test]
    public async Task DatesForAnItemThatHasSome_AreOnlyProposed()
    {
        var item = _weaver.AddItem("Kitchen quotes", start: new DateOnly(2026, 12, 1), end: new DateOnly(2026, 12, 3));
        _weaver.Mention(item, "@claude-research move this to next week");
        _model.Answer = _ => """{"reply":"Next week works.","start":"2026-11-16","end":"2026-11-20"}""";

        await _run.RunAsync(CancellationToken.None);

        var reply = _weaver.Replies.Single().Body;
        Assert.Multiple(() =>
        {
            Assert.That(_weaver.DateWrites, Is.Empty);
            Assert.That(reply, Does.Contain("Proposed").And.Contain("2026-11-16 to 2026-11-20"));
        });
    }

    [Test]
    public async Task AnAnswerThatFailsTheChecks_StillGetsAReply_AndNothingElseIsWritten()
    {
        var item = _weaver.AddItem("Kitchen quotes");
        _weaver.Mention(item, "@claude-research schedule this");
        _model.Answer = _ => """{"reply":"Done.","subItems":["Call"],"start":"2020-01-01"}""";

        var summary = await _run.RunAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_weaver.SubItems, Is.Empty);
            Assert.That(_weaver.DateWrites, Is.Empty);
            Assert.That(_weaver.Replies.Single().Body, Does.StartWith("I couldn't answer this request").And.Contain("before today"));
            Assert.That(_weaver.Pending, Is.Empty);
            Assert.That(summary.Rejected, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task APromptTheModelTurnsDown_StillGetsAReply()
    {
        var item = _weaver.AddItem("Kitchen quotes");
        _weaver.Mention(item, "@claude-research read this huge thread");
        _model.Answer = _ => throw new ModelRejectedException("413 the prompt is too long");

        var summary = await _run.RunAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_weaver.Replies.Single().Body, Does.StartWith("I couldn't answer this request").And.Contain("too long"));
            Assert.That(summary.Failed, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AnItemWeaverWontRead_StillGetsAReply()
    {
        var item = _weaver.AddItem("Kitchen quotes");
        _weaver.Mention(item, "@claude-research help");
        _weaver.RejectItemReadsFor.Add(item.Id);

        var summary = await _run.RunAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_weaver.Replies.Single().Body, Does.StartWith("I couldn't answer this request"));
            Assert.That(_model.Prompts, Is.Empty);
            Assert.That(summary.Failed, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AnAdditionWeaverRefuses_IsReportedInTheReply_AndTheRestStillHappens()
    {
        var item = _weaver.AddItem("Kitchen quotes");
        _weaver.Mention(item, "@claude-research add links");
        _weaver.RejectUrls.Add("https://dup.example/");
        _model.Answer = _ =>
            """{"reply":"Two links.","links":[{"url":"https://dup.example/"},{"url":"https://new.example/"}]}""";

        var summary = await _run.RunAsync(CancellationToken.None);

        var reply = _weaver.Replies.Single().Body;
        Assert.Multiple(() =>
        {
            Assert.That(_weaver.UrlLinks.Single().Url, Is.EqualTo("https://new.example/"));
            Assert.That(reply, Does.Contain("Not done").And.Contain("https://dup.example/").And.Contain("already on the item"));
            Assert.That(summary.Written, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AReplyWeaverRefuses_IsCountedAsAFailedWrite_AndTheRunCarriesOn()
    {
        var refused = _weaver.AddItem("Gone");
        var fine = _weaver.AddItem("Kitchen quotes");
        _weaver.Mention(refused, "@claude-research one");
        _weaver.Mention(fine, "@claude-research two");
        _weaver.RejectRepliesTo.Add(refused.Id);
        _model.Answer = _ => """{"reply":"Answered."}""";

        var summary = await _run.RunAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_weaver.Replies.Select(r => r.WorkItemId), Is.EqualTo(new[] { fine.Id }));
            Assert.That(summary.WriteFailed, Is.EqualTo(1));
            Assert.That(summary.Written, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task EveryWriteForAMention_CarriesThatMentionsRunId()
    {
        var item = _weaver.AddItem("Kitchen quotes");
        var mention = _weaver.Mention(item, "@claude-research plan this");
        _model.Answer = _ =>
            """{"reply":"Planned.","subItems":["Call"],"links":[{"url":"https://a.example/"}],"start":"2026-11-16"}""";

        await _run.RunAsync(CancellationToken.None);

        var runId = $"mention {mention.Comment.Id}";
        Assert.Multiple(() =>
        {
            Assert.That(MentionsRun.RunIdFor(mention), Is.EqualTo(runId));
            Assert.That(_weaver.SubItems.Single().RunId, Is.EqualTo(runId));
            Assert.That(_weaver.UrlLinks.Single().RunId, Is.EqualTo(runId));
            Assert.That(_weaver.Replies.Single().RunId, Is.EqualTo(runId));
            Assert.That(_weaver.History.Where(h => h.Entry.Field == "startDate").Single().Entry.RunId, Is.EqualTo(runId));
        });
    }

    [Test]
    public async Task ARetryAfterARefusedReply_AddsNothingAgain_AndItsReplyListsWhatTheFirstAttemptAdded()
    {
        var item = _weaver.AddItem("Kitchen quotes");
        _weaver.Mention(item, "@claude-research plan this for next week");
        _weaver.RejectRepliesTo.Add(item.Id);
        _model.Answer = _ =>
            """
            {"reply":"Here's a plan.",
             "subItems":["Call Fitters Ltd","Call Kitchens R Us"],
             "links":[{"url":"https://fitters.example/quote","label":"Fitters Ltd"}],
             "start":"2026-11-16","end":"2026-11-20"}
            """;
        var first = await _run.RunAsync(CancellationToken.None);

        _weaver.RejectRepliesTo.Clear();
        // A retry asks the model again, which may well word its answer differently.
        _model.Answer = _ =>
            """
            {"reply":"Here's the plan again.",
             "subItems":["Phone Fitters Ltd for a quote"],
             "links":[{"url":"https://kitchens.example/"}],
             "start":"2026-11-17","end":"2026-11-21"}
            """;
        var retry = await _run.RunAsync(CancellationToken.None);

        var reply = _weaver.Replies.Single().Body;
        Assert.Multiple(() =>
        {
            Assert.That(first.WriteFailed, Is.EqualTo(1));
            Assert.That(retry.Written, Is.EqualTo(1));
            Assert.That(_weaver.SubItems.Select(s => s.Title), Is.EqualTo(new[] { "Call Fitters Ltd", "Call Kitchens R Us" }));
            Assert.That(_weaver.UrlLinks.Select(l => l.Url), Is.EqualTo(new[] { "https://fitters.example/quote" }));
            Assert.That(_weaver.DateWrites, Has.Count.EqualTo(1));
            Assert.That(reply, Does.StartWith("Here's the plan again."));
            Assert.That(reply, Does.Contain("Added:\n- dates 2026-11-16 to 2026-11-20"));
            Assert.That(reply, Does.Contain("#101 Call Fitters Ltd").And.Contain("#102 Call Kitchens R Us"));
            Assert.That(reply, Does.Contain("link Fitters Ltd: https://fitters.example/quote"));
            Assert.That(reply, Does.Not.Contain("Proposed").And.Not.Contain("Not done"));
            Assert.That(reply, Does.Not.Contain("Phone Fitters").And.Not.Contain("kitchens.example").And.Not.Contain("2026-11-17"));
            Assert.That(_weaver.Pending, Is.Empty);
        });
    }

    [Test]
    public async Task ARetryAfterAnAttemptThatStoppedPartWay_AddsWhatThatAttemptDidntGetTo()
    {
        var item = _weaver.AddItem("Kitchen quotes");
        var mention = _weaver.Mention(item, "@claude-research plan this");
        await _weaver.SetDatesAsync(
            item.Id, new DateOnly(2026, 11, 16), null, item.Version, MentionsRun.ActorLabel, MentionsRun.RunIdFor(mention), CancellationToken.None);
        _model.Answer = _ => """{"reply":"Planned.","subItems":["Call"],"start":"2026-11-16"}""";

        await _run.RunAsync(CancellationToken.None);

        var reply = _weaver.Replies.Single().Body;
        Assert.Multiple(() =>
        {
            Assert.That(_weaver.DateWrites, Has.Count.EqualTo(1));
            Assert.That(_weaver.SubItems.Single().Title, Is.EqualTo("Call"));
            Assert.That(reply, Does.Contain("dates 2026-11-16 to ?").And.Contain("#101 Call"));
            Assert.That(reply, Does.Not.Contain("Proposed"));
        });
    }

    [Test]
    public async Task ARetryWhoseAnswerFails_StillListsWhatTheFirstAttemptAdded()
    {
        var item = _weaver.AddItem("Kitchen quotes");
        _weaver.Mention(item, "@claude-research add a step");
        _weaver.RejectRepliesTo.Add(item.Id);
        _model.Answer = _ => """{"reply":"Added.","subItems":["Call"]}""";
        await _run.RunAsync(CancellationToken.None);

        _weaver.RejectRepliesTo.Clear();
        _model.Answer = _ => "not JSON";
        var retry = await _run.RunAsync(CancellationToken.None);

        var reply = _weaver.Replies.Single().Body;
        Assert.Multiple(() =>
        {
            Assert.That(retry.Rejected, Is.EqualTo(1));
            Assert.That(reply, Does.StartWith("I couldn't answer this request").And.Contain("Added:\n- sub-item #101 Call"));
        });
    }

    [Test]
    public async Task SomeoneElsesSubItem_IsntTakenForAnEarlierAttempts()
    {
        var item = _weaver.AddItem("Kitchen quotes");
        _weaver.Mention(item, "@claude-research add a step");
        _weaver.AddChildByHand(item.Id, "Riley's own step");
        _model.Answer = _ => """{"reply":"Added.","subItems":["Call"]}""";

        await _run.RunAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_weaver.SubItems.Single().Title, Is.EqualTo("Call"));
            Assert.That(_weaver.Replies.Single().Body, Does.Not.Contain("Riley's own step"));
        });
    }

    [Test]
    public async Task AMentionAddedBackAfterAReply_IsANewRequest()
    {
        var item = _weaver.AddItem("Kitchen quotes");
        var mention = _weaver.Mention(item, "@claude-research add a step");
        _model.Answer = _ => """{"reply":"Added.","subItems":["Call"]}""";
        await _run.RunAsync(CancellationToken.None);

        _weaver.Remention(item, mention.Comment);
        _model.Answer = _ => """{"reply":"Added another.","subItems":["Email"]}""";
        await _run.RunAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_weaver.SubItems.Select(s => s.Title), Is.EqualTo(new[] { "Call", "Email" }));
            Assert.That(_weaver.Replies[1].Body, Does.Not.Contain("Call"));
        });
    }

    [Test]
    public void AnUnreachableModel_StopsTheRun_WithoutReplying()
    {
        var item = _weaver.AddItem("Kitchen quotes");
        _weaver.Mention(item, "@claude-research help");
        _model.Answer = _ => throw new ModelUnavailableException("connection refused");

        Assert.ThrowsAsync<ModelUnavailableException>(() => _run.RunAsync(CancellationToken.None));
        Assert.That(_weaver.Replies, Is.Empty);
    }

    [Test]
    public async Task NoPendingMentions_IsAnEmptyRun()
    {
        var summary = await _run.RunAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Considered, Is.Zero);
            Assert.That(_model.Prompts, Is.Empty);
        });
    }
}
