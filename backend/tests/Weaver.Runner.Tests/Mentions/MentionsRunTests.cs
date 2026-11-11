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
