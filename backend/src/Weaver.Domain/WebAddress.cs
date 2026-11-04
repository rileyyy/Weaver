namespace Weaver.Domain;

public static class WebAddress
{
    /// <summary>
    /// Only absolute http and https addresses, so a stored link can't run script (javascript:)
    /// or reach local files when the app opens it.
    /// </summary>
    public static bool IsHttpOrHttps(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
