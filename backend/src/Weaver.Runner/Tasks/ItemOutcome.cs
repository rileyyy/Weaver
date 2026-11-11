namespace Weaver.Runner.Tasks;

/// <summary>What a run did with one item; <see cref="RunSummary"/> counts these.</summary>
public enum ItemOutcome
{
    Written,
    Skipped,
    Rejected,
    WriteFailed,
    Failed,
}
