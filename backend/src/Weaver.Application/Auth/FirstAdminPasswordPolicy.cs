namespace Weaver.Application.Auth;

/// <summary>
/// Startup check on <see cref="FirstAdminOptions.Password"/>. The development password is
/// committed to the repo, so an admin created with it outside Development would be open to
/// anyone who has read the source.
/// </summary>
public static class FirstAdminPasswordPolicy
{
    /// <summary>The dev password committed in appsettings.Development.json.</summary>
    public const string DevelopmentPassword = "development-only-admin-password";

    public static void EnsureUsable(string? password, bool isDevelopment)
    {
        if (!isDevelopment && password == DevelopmentPassword)
        {
            throw new InvalidOperationException(
                "FirstAdmin:Password is the public development password, which must never be used outside Development. " +
                "Set FirstAdmin__Password to a password of your own.");
        }
    }
}
