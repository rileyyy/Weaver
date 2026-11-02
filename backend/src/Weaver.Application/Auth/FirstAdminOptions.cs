namespace Weaver.Application.Auth;

/// <summary>
/// Bound from the "FirstAdmin" configuration section. Only an admin can create accounts, so
/// a fresh deployment needs this to get its first one; once any account exists it is ignored.
/// </summary>
public class FirstAdminOptions
{
    public const string SectionName = "FirstAdmin";

    public string? Username { get; set; }

    public string? Password { get; set; }
}
