namespace Weaver.Runner.Mentions;

/// <summary>What <see cref="MentionAnswerChecker"/> made of a model's answer: the answer, or why it was rejected.</summary>
public sealed record MentionAnswerCheck(MentionAnswer? Answer, string? RejectReason)
{
    public static MentionAnswerCheck Accept(MentionAnswer answer) => new(answer, null);

    public static MentionAnswerCheck Reject(string reason) => new(null, reason);
}
