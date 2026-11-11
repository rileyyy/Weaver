using Weaver.Runner.WhenResolution;

namespace Weaver.Runner.Tests.WhenResolution;

[TestFixture]
public class WhenAnswerCheckerTests
{
    private static readonly DateOnly Today = new(2026, 11, 11);
    private static readonly BlockerCandidate Drywall = new(Guid.NewGuid(), 41, "Drywall", "Home", "Kitchen");

    [Test]
    public void DatesAndConfidence_Resolve()
    {
        var check = Check("""{"start":"2026-12-01","end":"2026-12-06","blockedBy":null,"confidence":"high"}""");

        Assert.Multiple(() =>
        {
            Assert.That(check.Outcome, Is.EqualTo(WhenAnswerOutcome.Resolve));
            Assert.That(check.Answer, Is.EqualTo(new WhenAnswer(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 6), null, "high")));
        });
    }

    [TestCase("41")]
    [TestCase("\"41\"")]
    [TestCase("\"#41\"")]
    public void BlockedBy_AnOfferedNumber_ResolvesToThatCandidate(string blockedBy)
    {
        var check = Check($$"""{"start":null,"end":null,"blockedBy":{{blockedBy}},"confidence":"medium"}""");

        Assert.Multiple(() =>
        {
            Assert.That(check.Outcome, Is.EqualTo(WhenAnswerOutcome.Resolve));
            Assert.That(check.Answer!.BlockedBy, Is.EqualTo(Drywall));
            Assert.That(check.Answer.HasDates, Is.False);
        });
    }

    [Test]
    public void DatesAndABlocker_ResolveTogether()
    {
        var check = Check("""{"start":"2026-12-01","end":null,"blockedBy":41,"confidence":"low"}""");

        Assert.That(check.Answer, Is.EqualTo(new WhenAnswer(new DateOnly(2026, 12, 1), null, Drywall, "low")));
    }

    [Test]
    public void TodayIsNotInThePast()
    {
        var check = Check("""{"start":"2026-11-11","end":"2026-11-11","confidence":"high"}""");

        Assert.That(check.Outcome, Is.EqualTo(WhenAnswerOutcome.Resolve));
    }

    [Test]
    public void ConfidenceIsNormalisedToLowerCase()
    {
        var check = Check("""{"start":"2026-12-01","confidence":"HIGH"}""");

        Assert.That(check.Answer!.Confidence, Is.EqualTo("high"));
    }

    [Test]
    public void Skip_LeavesThePhraseForAHuman_WithTheModelsReason()
    {
        var check = Check("""{"skip":"ambiguous"}""");

        Assert.Multiple(() =>
        {
            Assert.That(check.Outcome, Is.EqualTo(WhenAnswerOutcome.Skip));
            Assert.That(check.Reason, Is.EqualTo("ambiguous"));
            Assert.That(check.Answer, Is.Null);
        });
    }

    [Test]
    public void ANullSkip_AlongsideAResolution_IsNotASkip()
    {
        var check = Check("""{"skip":null,"start":"2026-12-01","confidence":"high"}""");

        Assert.That(check.Outcome, Is.EqualTo(WhenAnswerOutcome.Resolve));
    }

    [Test]
    public void JsonInACodeFence_OrWithChatter_IsStillRead()
    {
        var check = Check("Sure!\n```json\n{\"start\":\"2026-12-01\",\"confidence\":\"high\"}\n```");

        Assert.That(check.Answer!.Start, Is.EqualTo(new DateOnly(2026, 12, 1)));
    }

    [TestCase("", TestName = "Empty")]
    [TestCase("I think next week", TestName = "NoJson")]
    [TestCase("{\"start\":", TestName = "TruncatedJson")]
    [TestCase("[1,2]", TestName = "NotAnObject")]
    public void Unreadable_IsRejected(string raw)
    {
        Assert.That(Check(raw).Outcome, Is.EqualTo(WhenAnswerOutcome.Reject));
    }

    [TestCase("""{"start":"2026-11-10","confidence":"high"}""", TestName = "StartInThePast")]
    [TestCase("""{"end":"2026-11-10","confidence":"high"}""", TestName = "EndInThePast")]
    [TestCase("""{"start":"2026-12-06","end":"2026-12-01","confidence":"high"}""", TestName = "EndBeforeStart")]
    [TestCase("""{"start":"12/01/2026","confidence":"high"}""", TestName = "DateNotIso")]
    [TestCase("""{"start":"2026-02-30","confidence":"high"}""", TestName = "DateDoesNotExist")]
    [TestCase("""{"start":20261201,"confidence":"high"}""", TestName = "DateNotAString")]
    [TestCase("""{"blockedBy":99,"confidence":"high"}""", TestName = "BlockerNotOffered")]
    [TestCase("""{"blockedBy":"drywall","confidence":"high"}""", TestName = "BlockerNotANumber")]
    [TestCase("""{"start":"2026-12-01"}""", TestName = "NoConfidence")]
    [TestCase("""{"start":"2026-12-01","confidence":"certain"}""", TestName = "UnknownConfidence")]
    [TestCase("""{"start":null,"end":null,"blockedBy":null,"confidence":"high"}""", TestName = "NothingToWrite")]
    [TestCase("""{"skip":""}""", TestName = "EmptySkip")]
    public void AFailedCheck_RejectsTheWholeAnswer(string raw)
    {
        var check = Check(raw);

        Assert.Multiple(() =>
        {
            Assert.That(check.Outcome, Is.EqualTo(WhenAnswerOutcome.Reject));
            Assert.That(check.Answer, Is.Null);
            Assert.That(check.Reason, Is.Not.Empty);
        });
    }

    private static WhenAnswerCheck Check(string raw) => WhenAnswerChecker.Check(raw, Today, [Drywall]);
}
