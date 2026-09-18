using Weaver.Domain;
using Xunit;

namespace Weaver.Domain.Tests;

public class RankCalculatorTests
{
    [Fact]
    public void EmptyCell_ReturnsDefaultRank()
    {
        var rank = RankCalculator.GetRankBetween(null, null);

        Assert.Equal(1.0, rank);
    }

    [Fact]
    public void InsertAtStart_ReturnsRankBelowNext()
    {
        var rank = RankCalculator.GetRankBetween(null, 5.0);

        Assert.True(rank < 5.0);
    }

    [Fact]
    public void InsertAtEnd_ReturnsRankAboveLast()
    {
        var rank = RankCalculator.GetRankBetween(5.0, null);

        Assert.True(rank > 5.0);
    }

    [Fact]
    public void InsertBetween_ReturnsMidpoint()
    {
        var rank = RankCalculator.GetRankBetween(2.0, 4.0);

        Assert.Equal(3.0, rank);
    }

    [Fact]
    public void RepeatedInsertsBetweenSameNeighbors_StayOrdered()
    {
        var previous = 0.0;
        var next = 1.0;

        for (var i = 0; i < 20; i++)
        {
            var rank = RankCalculator.GetRankBetween(previous, next);

            Assert.True(rank > previous);
            Assert.True(rank < next);

            next = rank;
        }
    }

    [Fact]
    public void PreviousNotBeforeNext_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RankCalculator.GetRankBetween(5.0, 5.0));
        Assert.Throws<ArgumentException>(() => RankCalculator.GetRankBetween(6.0, 5.0));
    }
}
