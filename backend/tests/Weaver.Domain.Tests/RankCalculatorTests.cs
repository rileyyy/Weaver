using Weaver.Domain;

namespace Weaver.Domain.Tests;

[TestFixture]
public class RankCalculatorTests
{
    [Test]
    public void EmptyCell_ReturnsDefaultRank()
    {
        var rank = RankCalculator.GetRankBetween(null, null);

        Assert.That(rank, Is.EqualTo(1.0));
    }

    [Test]
    public void InsertAtStart_ReturnsRankBelowNext()
    {
        var rank = RankCalculator.GetRankBetween(null, 5.0);

        Assert.That(rank, Is.LessThan(5.0));
    }

    [Test]
    public void InsertAtEnd_ReturnsRankAboveLast()
    {
        var rank = RankCalculator.GetRankBetween(5.0, null);

        Assert.That(rank, Is.GreaterThan(5.0));
    }

    [Test]
    public void InsertBetween_ReturnsMidpoint()
    {
        var rank = RankCalculator.GetRankBetween(2.0, 4.0);

        Assert.That(rank, Is.EqualTo(3.0));
    }

    [Test]
    public void RepeatedInsertsBetweenSameNeighbors_StayOrdered()
    {
        var previous = 0.0;
        var next = 1.0;

        for (var i = 0; i < 20; i++)
        {
            var rank = RankCalculator.GetRankBetween(previous, next);

            Assert.That(rank, Is.GreaterThan(previous));
            Assert.That(rank, Is.LessThan(next));

            next = rank;
        }
    }

    [TestCase(5.0, 5.0)]
    [TestCase(6.0, 5.0)]
    public void PreviousNotBeforeNext_ThrowsArgumentException(double previous, double next)
    {
        Assert.Throws<ArgumentException>(() => RankCalculator.GetRankBetween(previous, next));
    }
}
