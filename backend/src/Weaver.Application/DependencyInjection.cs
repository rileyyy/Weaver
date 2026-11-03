using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Weaver.Application.Auth;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddWeaverApplication(this IServiceCollection services) =>
        services
            .AddScoped<IWorkItemService, WorkItemService>()
            .AddScoped<IAuthService, AuthService>()
            .AddScoped<IUserService, UserService>()
            .AddScoped<IAdminAuthorizer, AdminAuthorizer>()
            .AddScoped<IUserAdministrationService, UserAdministrationService>()
            .AddScoped<IAgentAccountService, AgentAccountService>()
            .AddScoped<IApiKeyAuthenticator, ApiKeyAuthenticator>()
            .AddScoped<ICallerIdentityService, CallerIdentityService>()
            .AddScoped<IFirstAdminBootstrapper, FirstAdminBootstrapper>()
            .AddScoped<ICommentService, CommentService>()
            .AddScoped<IWorkItemLinkService, WorkItemLinkService>()
            .AddScoped<IWorkItemHistoryService, WorkItemHistoryService>()
            .AddScoped<IWorkItemChangesService, WorkItemChangesService>()
            .AddScoped<ISpaceService, SpaceService>()
            .AddScoped<IStatusService, StatusService>()
            .AddScoped<IWorkItemLayerService, WorkItemLayerService>()
            .AddScoped<IWorkItemPriorityService, WorkItemPriorityService>()
            .AddScoped<IWorkItemRecurrenceService, WorkItemRecurrenceService>()
            .AddScoped<IServerSettingsService, ServerSettingsService>()
            .AddScoped<IServerCalendar, ServerCalendar>()
            .AddScoped<IQuickPickService, QuickPickService>()
            .AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>()
            .AddTimeProvider();

    private static IServiceCollection AddTimeProvider(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
