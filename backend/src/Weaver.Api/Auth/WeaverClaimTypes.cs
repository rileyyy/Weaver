namespace Weaver.Api.Auth;

/// <summary>Claims Weaver adds to a principal beyond the standard JWT ones.</summary>
public static class WeaverClaimTypes
{
    /// <summary>The id of the API key an agent signed in with. Absent for access tokens.</summary>
    public const string ApiKeyId = "weaver_api_key_id";

    /// <summary>
    /// The scope of that key, read from its row as the request was signed in, so checking a
    /// write needs no second lookup. Absent for access tokens.
    /// </summary>
    public const string ApiKeyScope = "weaver_api_key_scope";
}
