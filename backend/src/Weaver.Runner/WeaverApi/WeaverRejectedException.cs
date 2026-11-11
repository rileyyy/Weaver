namespace Weaver.Runner.WeaverApi;

/// <summary>Weaver turned down one write (e.g. a version conflict or an existing link).</summary>
public sealed class WeaverRejectedException(string message) : Exception(message);
