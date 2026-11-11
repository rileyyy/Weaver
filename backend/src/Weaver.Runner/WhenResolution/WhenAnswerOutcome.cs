namespace Weaver.Runner.WhenResolution;

public enum WhenAnswerOutcome
{
    /// <summary>The answer is safe to write.</summary>
    Resolve,

    /// <summary>The model chose to leave the phrase for a human.</summary>
    Skip,

    /// <summary>The answer failed a check, so nothing of it is written.</summary>
    Reject,
}
