namespace Weaver.Runner.WeaverApi;

/// <summary>Weaver can't be reached, or refused the agent key: the run can't go on.</summary>
public sealed class WeaverUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
