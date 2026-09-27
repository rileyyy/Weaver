namespace Weaver.Domain;

/// <summary>
/// Computes fractional sort ranks so a card can be dropped between two siblings
/// without rewriting every other row's rank. Ranks only need to be locally
/// consistent within one board cell (parent + status). When two siblings end
/// up with the same rank (concurrent inserts at one spot, or exhausted double
/// precision), the caller respaces the cell with <see cref="EvenlySpaced"/>.
/// </summary>
public static class RankCalculator
{
    private const double DefaultStep = 1.0;

    public static double GetRankBetween(double? previous, double? next)
    {
        if (previous is null && next is null)
        {
            return DefaultStep;
        }

        if (previous is null)
        {
            return next!.Value - DefaultStep;
        }

        if (next is null)
        {
            return previous.Value + DefaultStep;
        }

        if (previous.Value >= next.Value)
        {
            throw new ArgumentException(
                $"previous rank ({previous.Value}) must be less than next rank ({next.Value}).",
                nameof(previous));
        }

        return previous.Value + (next.Value - previous.Value) / 2;
    }

    /// <summary>Fresh, strictly increasing ranks for a cell of <paramref name="count"/> items.</summary>
    public static IReadOnlyList<double> EvenlySpaced(int count) =>
        Enumerable.Range(1, count).Select(i => i * DefaultStep).ToList();
}
