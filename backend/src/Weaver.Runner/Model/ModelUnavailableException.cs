namespace Weaver.Runner.Model;

public sealed class ModelUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
