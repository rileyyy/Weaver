namespace Weaver.Runner.Model;

/// <summary>Why a model's answer failed a check; caught inside the checker that threw it.</summary>
internal sealed class InvalidAnswerException(string message) : Exception(message);
