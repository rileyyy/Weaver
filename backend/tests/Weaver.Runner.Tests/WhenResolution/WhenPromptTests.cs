using Weaver.Runner.WeaverApi;
using Weaver.Runner.WhenResolution;

namespace Weaver.Runner.Tests.WhenResolution;

[TestFixture]
public class WhenPromptTests
{
    private static readonly DateOnly Today = new(2026, 11, 11);
    private static readonly QuickPickRange[] QuickPicks = [new("NextWeek", new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 22))];

    [Test]
    public void AnItemWithAParent_NamesItsParent()
    {
        var prompt = WhenPrompt.Build(Item(parentNumber: 40, parentTitle: "Kitchen"), Today, QuickPicks, []);

        Assert.That(prompt.User, Does.Contain("Parent: #40 Kitchen"));
    }

    [Test]
    public void AnItemWithoutAParent_HasNoParentLine()
    {
        var prompt = WhenPrompt.Build(Item(), Today, QuickPicks, []);

        Assert.That(prompt.User, Does.Not.Contain("Parent:"));
    }

    [Test]
    public void NoCandidates_SaysSo()
    {
        var prompt = WhenPrompt.Build(Item(), Today, QuickPicks, []);

        Assert.That(prompt.User, Does.Contain("Candidates it could wait for: none"));
    }

    [Test]
    public void EachCandidate_IsListedByNumber_WithItsSpaceAndAnyParent()
    {
        BlockerCandidate[] candidates =
        [
            new(Guid.NewGuid(), 41, "Drywall", "Home", "Kitchen"),
            new(Guid.NewGuid(), 52, "Drywall the garage", "Garage", null),
        ];

        var prompt = WhenPrompt.Build(Item(), Today, QuickPicks, candidates);

        Assert.That(prompt.User, Does.Contain("- #41 Drywall (Space: Home, parent: Kitchen)")
            .And.Contain("- #52 Drywall the garage (Space: Garage)"));
    }

    [Test]
    public void TodayAndQuickPicks_AreIsoDates()
    {
        var prompt = WhenPrompt.Build(Item(), Today, QuickPicks, []);

        Assert.That(prompt.User, Does.Contain("Today: 2026-11-11 (Wednesday)")
            .And.Contain("- NextWeek: 2026-11-16 to 2026-11-22"));
    }

    private static WeaverWorkItem Item(int? parentNumber = null, string? parentTitle = null) =>
        new(Guid.NewGuid(), 42, "Paint the walls", "after drywall", Guid.NewGuid(), "Home",
            parentNumber, parentTitle, null, null, 3);
}
