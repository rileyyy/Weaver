namespace Weaver.Runner.WhenResolution;

/// <summary>
/// Settings only the When task has.
/// </summary>
/// <param name="ReplaceDatesWhenPhraseEdited">
/// Also resolve items whose When a human edited after their dates were set, replacing those
/// dates (ADR-0010's one exception to "only resolve or add", ticket 49). Off by default, so a
/// runner never changes existing dates unless it's told to. Links are never removed either way.
/// </param>
public sealed record WhenResolutionOptions(bool ReplaceDatesWhenPhraseEdited = false);
