using Weaver.Application.Auth;
using Weaver.Application.Services;

namespace Weaver.Api.Startup;

public static class FirstAdminStartup
{
    public static async Task EnsureFirstAdminAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<IFirstAdminBootstrapper>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(FirstAdminStartup));

        var result = await bootstrapper.EnsureFirstAdminAsync(ct);
        switch (result)
        {
            case FirstAdminBootstrapResult.Created:
                logger.LogInformation("Created the first admin account from the {Section} settings.", FirstAdminOptions.SectionName);
                break;
            case FirstAdminBootstrapResult.NotConfigured:
                logger.LogWarning(
                    "There are no accounts and nobody can register. Set {Section}__Username and {Section}__Password and restart to create the first admin.",
                    FirstAdminOptions.SectionName,
                    FirstAdminOptions.SectionName);
                break;
        }
    }
}
