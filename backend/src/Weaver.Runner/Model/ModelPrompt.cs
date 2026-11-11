namespace Weaver.Runner.Model;

/// <summary>One question for the model: standing instructions and the item-specific context.</summary>
public sealed record ModelPrompt(string System, string User);
