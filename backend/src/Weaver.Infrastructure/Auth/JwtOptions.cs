namespace Weaver.Infrastructure.Auth;

/// <summary>
/// Bound from the "Jwt" configuration section. Only <see cref="SigningKey"/>
/// needs to be supplied per-deployment (like <c>POSTGRES_PASSWORD</c>, it has
/// no default and startup should fail loudly without one) — issuer/audience
/// only matter for validating tokens this same API issued, so fixed values
/// are fine.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "weaver-api";

    public string Audience { get; set; } = "weaver-client";

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);
}
