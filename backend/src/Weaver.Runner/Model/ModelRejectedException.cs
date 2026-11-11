namespace Weaver.Runner.Model;

/// <summary>
/// The model endpoint turned down one prompt (e.g. too long for its context window), so only the
/// item that prompt was about is skipped.
/// </summary>
public sealed class ModelRejectedException(string message) : Exception(message);
