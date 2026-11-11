namespace Weaver.Runner.WeaverApi;

/// <summary>
/// Weaver turned down a request about one item (e.g. a version conflict, an existing link, or
/// an item that has gone), so only that item is skipped.
/// </summary>
public sealed class WeaverRejectedException(string message) : Exception(message);
