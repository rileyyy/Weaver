namespace Weaver.Domain;

public static class WebAddress
{
    /// <summary>
    /// Only absolute http and https addresses, so a stored link can't run script (javascript:)
    /// or reach local files when the app opens it. A host is required too, as the client checks,
    /// since an address like <c>http:///x</c> parses but leads nowhere.
    /// </summary>
    public static bool IsHttpOrHttps(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
        uri.Host.Length > 0;
}
