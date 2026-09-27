namespace Weaver.Api.RateLimiting;

/// <summary>Bound from the "RateLimiting:Auth" configuration section.</summary>
public class AuthRateLimitOptions
{
    public const string SectionName = "RateLimiting:Auth";

    /// <summary>
    /// Requests each client IP may make to the anonymous auth endpoints per window. Generous
    /// enough for a team behind one NAT signing in at once; the per-account lockout is what
    /// stops password guessing against a single user.
    /// </summary>
    public int PermitLimit { get; set; } = 30;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}
