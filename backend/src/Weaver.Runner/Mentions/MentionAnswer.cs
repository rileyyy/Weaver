namespace Weaver.Runner.Mentions;

/// <summary>
/// A checked answer to one mention: every value in it has passed <see cref="MentionAnswerChecker"/>.
/// Whether the dates may be written depends on the item, so <see cref="MentionsRun"/> decides that.
/// </summary>
public sealed record MentionAnswer(
    string Reply,
    IReadOnlyList<string> SubItems,
    IReadOnlyList<MentionLink> Links,
    DateOnly? Start,
    DateOnly? End)
{
    public bool HasDates => Start is not null || End is not null;
}
