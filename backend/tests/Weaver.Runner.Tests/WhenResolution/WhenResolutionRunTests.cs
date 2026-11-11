using Microsoft.Extensions.Logging.Abstractions;
using Weaver.Runner.Model;
using Weaver.Runner.Tasks;
using Weaver.Runner.Tests.Fakes;
using Weaver.Runner.WeaverApi;
using Weaver.Runner.WhenResolution;

namespace Weaver.Runner.Tests.WhenResolution;

[TestFixture]
public class WhenResolutionRunTests
{
    private FakeWeaverClient _weaver = null!;
    private FakeModelClient _model = null!;

    [SetUp]
    public void SetUp()
    {
        _weaver = new FakeWeaverClient();
        _model = new FakeModelClient();
    }

    [Test]
    public async Task DatesFromTheModel_AreWritten_AtTheVersionRead_LabelledWithTaskAndConfidence()
    {
        var gutters = _weaver.Add("Clean the gutters", when: "next week");
        _model.Answer = _ => """{"start":"2026-11-16","end":"2026-11-22","blockedBy":null,"confidence":"high"}""";

        var summary = await RunAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_weaver.DateWrites, Is.EqualTo(new[]
            {
                new DateWrite(gutters.Id, new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 22), gutters.Version,
                    "Runner when-resolution, confidence high"),
            }));
            Assert.That(_weaver.LinkWrites, Is.Empty);
            Assert.That(summary, Is.EqualTo(new RunSummary(Considered: 1, Written: 1, Skipped: 0, Rejected: 0, WriteFailed: 0, Failed: 0)));
        });
    }

    [Test]
    public async Task ABlockerFromTheModel_IsAddedAsABlockedByLink()
    {
        var drywall = _weaver.Add("Drywall");
        var paint = _weaver.Add("Paint the walls", when: "after drywall");
        _model.Answer = _ => $$"""{"blockedBy":{{drywall.Number}},"confidence":"medium"}""";

        await RunAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_weaver.LinkWrites, Is.EqualTo(new[]
            {
                new LinkWrite(paint.Id, drywall.Id, "Runner when-resolution, confidence medium"),
            }));
            Assert.That(_weaver.DateWrites, Is.Empty);
        });
    }

    [Test]
    public async Task TheModelIsAskedAboutOneItemAtATime_WithItsContext()
    {
        var drywall = _weaver.Add("Drywall");
        _weaver.Add("Paint the walls", when: "after drywall");
        _weaver.Add("Clean the gutters", when: "next week");

        await RunAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_model.Prompts, Has.Count.EqualTo(2));
            Assert.That(_model.Prompts[0].User, Does.Contain("Paint the walls").And.Contain("after drywall")
                .And.Contain($"#{drywall.Number} Drywall").And.Not.Contain("Clean the gutters"));
            Assert.That(_model.Prompts[1].User, Does.Contain("Clean the gutters").And.Not.Contain("Paint the walls"));
            Assert.That(_model.Prompts[1].User, Does.Contain("2026-11-11").And.Contain("NextWeek: 2026-11-16 to 2026-11-22"));
        });
    }

    [Test]
    public async Task AnItemThatAlreadyHasDates_IsNeverWritten_OrAskedAbout()
    {
        _weaver.Add("Clean the gutters", when: "next week", start: new DateOnly(2026, 12, 1));
        _model.Answer = _ => """{"start":"2026-11-16","confidence":"high"}""";

        var summary = await RunAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_model.Prompts, Is.Empty);
            Assert.That(_weaver.DateWrites, Is.Empty);
            Assert.That(summary.Skipped, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task OnlyTheDatesTheModelGave_AreSent()
    {
        var gutters = _weaver.Add("Clean the gutters", when: "from next week");
        _model.Answer = _ => """{"start":"2026-11-16","confidence":"low"}""";

        await RunAsync();

        Assert.That(_weaver.DateWrites.Single(), Is.EqualTo(
            new DateWrite(gutters.Id, new DateOnly(2026, 11, 16), null, gutters.Version, "Runner when-resolution, confidence low")));
    }

    [Test]
    public async Task ABadAnswerForOneItem_IsSkipped_AndTheRunCarriesOn()
    {
        _weaver.Add("Paint the walls", when: "after drywall");
        var gutters = _weaver.Add("Clean the gutters", when: "next week");
        _model.AnswerByTitle(new()
        {
            ["Paint the walls"] = """{"start":"2026-11-01","end":"2026-11-05","blockedBy":99,"confidence":"high"}""",
            ["Clean the gutters"] = """{"start":"2026-11-16","confidence":"high"}""",
        });

        var summary = await RunAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_weaver.DateWrites.Select(w => w.WorkItemId), Is.EqualTo(new[] { gutters.Id }));
            Assert.That(_weaver.LinkWrites, Is.Empty);
            Assert.That(summary, Is.EqualTo(new RunSummary(Considered: 2, Written: 1, Skipped: 0, Rejected: 1, WriteFailed: 0, Failed: 0)));
        });
    }

    [Test]
    public async Task AModelSkip_WritesNothing()
    {
        _weaver.Add("Bake", when: "sometime");
        _model.Answer = _ => """{"skip":"ambiguous"}""";

        var summary = await RunAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_weaver.DateWrites, Is.Empty);
            Assert.That(_weaver.LinkWrites, Is.Empty);
            Assert.That(summary.Skipped, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ARejectedDateWrite_SkipsThatItemsLink_AndTheRunCarriesOn()
    {
        var drywall = _weaver.Add("Drywall");
        var paint = _weaver.Add("Paint the walls", when: "after drywall");
        var gutters = _weaver.Add("Clean the gutters", when: "next week");
        _weaver.RejectWritesTo.Add(paint.Id);
        _model.AnswerByTitle(new()
        {
            ["Paint the walls"] = $$"""{"start":"2026-11-16","blockedBy":{{drywall.Number}},"confidence":"high"}""",
            ["Clean the gutters"] = """{"start":"2026-11-16","confidence":"high"}""",
        });

        var summary = await RunAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_weaver.LinkWrites, Is.Empty);
            Assert.That(_weaver.DateWrites.Select(w => w.WorkItemId), Is.EqualTo(new[] { gutters.Id }));
            Assert.That(summary, Is.EqualTo(new RunSummary(Considered: 2, Written: 1, Skipped: 0, Rejected: 0, WriteFailed: 1, Failed: 0)));
        });
    }

    [Test]
    public async Task AReadWeaverTurnsDownForOneItem_FailsThatItem_AndTheRunCarriesOn()
    {
        var paint = _weaver.Add("Paint the walls", when: "after drywall");
        var gutters = _weaver.Add("Clean the gutters", when: "next week");
        _weaver.RejectLinkReadsFor.Add(paint.Id);
        _model.Answer = _ => """{"start":"2026-11-16","confidence":"high"}""";

        var summary = await RunAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_model.Prompts, Has.Count.EqualTo(1));
            Assert.That(_weaver.DateWrites.Select(w => w.WorkItemId), Is.EqualTo(new[] { gutters.Id }));
            Assert.That(summary, Is.EqualTo(new RunSummary(Considered: 2, Written: 1, Skipped: 0, Rejected: 0, WriteFailed: 0, Failed: 1)));
        });
    }

    [Test]
    public async Task APromptTheModelTurnsDown_FailsThatItem_AndTheRunCarriesOn()
    {
        _weaver.Add("Paint the walls", when: "after drywall");
        var gutters = _weaver.Add("Clean the gutters", when: "next week");
        _model.Answer = prompt => prompt.User.Contains("Paint the walls", StringComparison.Ordinal)
            ? throw new ModelRejectedException("400 Bad Request: prompt too long")
            : """{"start":"2026-11-16","confidence":"high"}""";

        var summary = await RunAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_weaver.DateWrites.Select(w => w.WorkItemId), Is.EqualTo(new[] { gutters.Id }));
            Assert.That(summary, Is.EqualTo(new RunSummary(Considered: 2, Written: 1, Skipped: 0, Rejected: 0, WriteFailed: 0, Failed: 1)));
        });
    }

    [Test]
    public void AnUnreachableModel_StopsTheRun_BeforeAskingAboutTheNextItem()
    {
        _weaver.Add("Paint the walls", when: "after drywall");
        _weaver.Add("Clean the gutters", when: "next week");
        _model.Answer = _ => throw new ModelUnavailableException("connection refused");

        Assert.ThrowsAsync<ModelUnavailableException>(() => RunAsync());
        Assert.Multiple(() =>
        {
            Assert.That(_model.Prompts, Has.Count.EqualTo(1));
            Assert.That(_weaver.DateWrites, Is.Empty);
        });
    }

    [Test]
    public void AnUnreachableWeaver_StopsTheRun()
    {
        _weaver.FailEveryCallWith = new WeaverUnavailableException("connection refused");

        Assert.ThrowsAsync<WeaverUnavailableException>(() => RunAsync());
    }

    [Test]
    public async Task ByDefault_EditedItemsAreNotAskedFor_AndTheirDatesAreNeverReplaced()
    {
        _weaver.Add("Fence", when: "next summer", start: new DateOnly(2027, 3, 20), editedSinceResolved: true);
        _model.Answer = _ => """{"start":"2027-06-20","confidence":"high"}""";

        var summary = await RunAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_weaver.UnresolvedListings, Is.EqualTo(new[] { false }));
            Assert.That(_model.Prompts, Is.Empty);
            Assert.That(_weaver.DateWrites, Is.Empty);
            Assert.That(summary.Skipped, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task WithReplaceDatesWhenPhraseEdited_AnEditedItemsDatesAreReplaced_BothOfThem()
    {
        var fence = _weaver.Add(
            "Fence", when: "from next summer", start: new DateOnly(2027, 3, 20), end: new DateOnly(2027, 3, 22),
            editedSinceResolved: true);
        _model.Answer = _ => """{"start":"2027-06-20","confidence":"high"}""";

        var summary = await RunAsync(replaceDatesWhenPhraseEdited: true);

        Assert.Multiple(() =>
        {
            Assert.That(_weaver.UnresolvedListings, Is.EqualTo(new[] { true }));
            Assert.That(_weaver.DateWrites, Is.EqualTo(new[]
            {
                new DateWrite(fence.Id, new DateOnly(2027, 6, 20), null, fence.Version,
                    "Runner when-resolution, confidence high", Replaces: true),
            }));
            Assert.That(summary.Written, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task WithReplaceDatesWhenPhraseEdited_ADatedItemThatIsNotEdited_IsNeverWritten_OrAskedAbout()
    {
        _weaver.Add("Fence", when: "next spring", start: new DateOnly(2027, 3, 20), editedSinceResolved: false);
        _model.Answer = _ => """{"start":"2027-06-20","confidence":"high"}""";

        var summary = await RunAsync(replaceDatesWhenPhraseEdited: true);

        Assert.Multiple(() =>
        {
            Assert.That(_model.Prompts, Is.Empty);
            Assert.That(_weaver.DateWrites, Is.Empty);
            Assert.That(summary.Skipped, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task WithReplaceDatesWhenPhraseEdited_AnUndatedItem_StillOnlyGetsTheDatesGiven()
    {
        var gutters = _weaver.Add("Clean the gutters", when: "from next week");
        _model.Answer = _ => """{"start":"2026-11-16","confidence":"low"}""";

        await RunAsync(replaceDatesWhenPhraseEdited: true);

        Assert.That(_weaver.DateWrites.Single(), Is.EqualTo(
            new DateWrite(gutters.Id, new DateOnly(2026, 11, 16), null, gutters.Version, "Runner when-resolution, confidence low")));
    }

    [Test]
    public async Task WithReplaceDatesWhenPhraseEdited_ABlockerForAnEditedItem_IsAdded_AndItsDatesKept()
    {
        var drywall = _weaver.Add("Drywall");
        var paint = _weaver.Add(
            "Paint the walls", when: "after drywall", start: new DateOnly(2027, 3, 20), editedSinceResolved: true);
        _model.Answer = _ => $$"""{"blockedBy":{{drywall.Number}},"confidence":"medium"}""";

        await RunAsync(replaceDatesWhenPhraseEdited: true);

        Assert.Multiple(() =>
        {
            Assert.That(_weaver.LinkWrites.Select(l => (l.WorkItemId, l.BlockerId)), Is.EqualTo(new[] { (paint.Id, drywall.Id) }));
            Assert.That(_weaver.DateWrites, Is.Empty);
        });
    }

    private Task<RunSummary> RunAsync(bool replaceDatesWhenPhraseEdited = false) =>
        new WhenResolutionRun(
                _weaver,
                _model,
                NullLogger<WhenResolutionRun>.Instance,
                new WhenResolutionOptions(replaceDatesWhenPhraseEdited))
            .RunAsync(CancellationToken.None);
}
