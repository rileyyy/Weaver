namespace Weaver.Runner.WhenResolution;

/// <summary>What <see cref="WhenAnswerChecker"/> made of a model's answer.</summary>
public sealed record WhenAnswerCheck(WhenAnswerOutcome Outcome, WhenAnswer? Answer, string? Reason)
{
    public static WhenAnswerCheck Resolve(WhenAnswer answer) => new(WhenAnswerOutcome.Resolve, answer, null);

    public static WhenAnswerCheck Skip(string reason) => new(WhenAnswerOutcome.Skip, null, reason);

    public static WhenAnswerCheck Reject(string reason) => new(WhenAnswerOutcome.Reject, null, reason);
}
