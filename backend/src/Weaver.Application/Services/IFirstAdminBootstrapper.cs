namespace Weaver.Application.Services;

/// <summary>
/// Creates the configured first admin on a database with no accounts, so a fresh deployment
/// can sign in although nobody can register.
/// </summary>
public interface IFirstAdminBootstrapper
{
    Task<FirstAdminBootstrapResult> EnsureFirstAdminAsync(CancellationToken ct = default);
}
