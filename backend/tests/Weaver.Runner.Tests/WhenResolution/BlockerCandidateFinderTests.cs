using Weaver.Runner.Tests.Fakes;
using Weaver.Runner.WhenResolution;

namespace Weaver.Runner.Tests.WhenResolution;

[TestFixture]
public class BlockerCandidateFinderTests
{
    private FakeWeaverClient _weaver = null!;

    [SetUp]
    public void SetUp() => _weaver = new FakeWeaverClient();

    [Test]
    public async Task APhraseNamingWork_FindsThatWork_ButNotTheItemItself()
    {
        var drywall = _weaver.Add("Drywall the kitchen");
        var paint = _weaver.Add("Paint the walls", when: "after drywall");

        var candidates = await FindAsync(paint);

        Assert.That(candidates.Select(c => c.Id), Is.EqualTo(new[] { drywall.Id }));
    }

    [Test]
    public async Task WordsAreSearchedOneAtATime_SoConnectingWordsDontHideMatches()
    {
        _weaver.Add("Drywall");
        var paint = _weaver.Add("Paint", when: "once the drywall is done");

        await FindAsync(paint);

        Assert.That(_weaver.Searches, Is.EqualTo(new[] { "drywall" }));
    }

    [Test]
    public async Task APhraseNamingOnlyATime_SearchesNothing()
    {
        _weaver.Add("Next spring planting");
        var gutters = _weaver.Add("Clean the gutters", when: "next spring");

        var candidates = await FindAsync(gutters);

        Assert.Multiple(() =>
        {
            Assert.That(candidates, Is.Empty);
            Assert.That(_weaver.Searches, Is.Empty);
        });
    }

    [Test]
    public async Task ItemsAlreadyLinked_AreNotOffered()
    {
        var drywall = _weaver.Add("Drywall");
        var paint = _weaver.Add("Paint", when: "after drywall");
        _weaver.Links.Add((drywall.Id, paint.Id));

        Assert.That(await FindAsync(paint), Is.Empty);
    }

    [Test]
    public async Task MoreMatchingWordsRankFirst_ThenTheSameSpace_ThenNumber()
    {
        var otherSpace = Guid.NewGuid();
        var christmasElsewhere = _weaver.Add("Christmas lights", spaceId: otherSpace);
        var christmas = _weaver.Add("Christmas cards");
        var market = _weaver.Add("Christmas market stall", spaceId: otherSpace);
        var bake = _weaver.Add("Bake", when: "before the Christmas market");

        var candidates = await FindAsync(bake);

        Assert.That(candidates.Select(c => c.Id), Is.EqualTo(new[] { market.Id, christmas.Id, christmasElsewhere.Id }));
    }

    [Test]
    public async Task AtMostTheLimitIsOffered()
    {
        for (var i = 0; i < 8; i++)
        {
            _weaver.Add($"Drywall part {i}");
        }

        var paint = _weaver.Add("Paint", when: "after drywall");

        Assert.That(await FindAsync(paint), Has.Count.EqualTo(BlockerCandidateFinder.MaxCandidates));
    }

    private Task<IReadOnlyList<BlockerCandidate>> FindAsync(Runner.WeaverApi.WeaverWorkItem item) =>
        new BlockerCandidateFinder(_weaver).FindAsync(item, CancellationToken.None);
}
