namespace Weaver.Runner.WhenResolution;

/// <summary>A checked resolution: every value in it has passed <see cref="WhenAnswerChecker"/>.</summary>
public sealed record WhenAnswer(DateOnly? Start, DateOnly? End, BlockerCandidate? BlockedBy, string Confidence)
{
    public bool HasDates => Start is not null || End is not null;
}
