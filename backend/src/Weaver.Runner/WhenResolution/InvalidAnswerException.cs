namespace Weaver.Runner.WhenResolution;

/// <summary>Why a model's answer failed a check; caught inside <see cref="WhenAnswerChecker"/>.</summary>
internal sealed class InvalidAnswerException(string message) : Exception(message);
