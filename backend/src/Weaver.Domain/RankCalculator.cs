namespace Weaver.Domain;

/// <summary>
/// Computes fractional sort ranks so a card can be dropped between two siblings
/// without rewriting every other row's rank. Ranks only need to be locally
/// consistent within one board cell (parent + status); if repeated inserts at
/// the same spot ever exhaust double precision, rebalancing the cell is a
/// separate, infrequent maintenance operation, not part of this calculation.
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
}
