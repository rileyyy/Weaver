using Weaver.Runner.WeaverApi;

namespace Weaver.Runner.Mentions;

/// <summary>
/// What an earlier attempt at answering the same mention already added. A run whose reply Weaver
/// refused, or that stopped before replying, leaves the mention pending, so a later run answers it
/// again; <see cref="MentionsRun"/> reports these additions instead of making them a second time.
/// <see cref="Dates"/> is null when the attempt didn't set any.
/// </summary>
public sealed record EarlierAttempt(
    (DateOnly? Start, DateOnly? End)? Dates,
    IReadOnlyList<WeaverSubItem> SubItems,
    IReadOnlyList<MentionLink> Links)
{
    public static readonly EarlierAttempt None = new(null, [], []);
}
