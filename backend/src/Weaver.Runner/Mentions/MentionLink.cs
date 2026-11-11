namespace Weaver.Runner.Mentions;

/// <summary>A URL link the model asked to add to the mentioned item.</summary>
public sealed record MentionLink(string Url, string? Label);
