using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Weaver.Application.Attachments;
using Weaver.Application.Auth;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddWeaverApplication(this IServiceCollection services) =>
        services
            .AddScoped<IWorkItemService, WorkItemService>()
            .AddScoped<IWorkItemSpaceMoveService, WorkItemSpaceMoveService>()
            .AddScoped<IWorkItemTrashService, WorkItemTrashService>()
            .AddScoped<IWorkItemPurgeService, WorkItemPurgeService>()
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
            .AddScoped<IWorkItemUrlLinkService, WorkItemUrlLinkService>()
            .AddScoped<IWorkItemAttachmentService, WorkItemAttachmentService>()
            .AddScoped<IAttachmentFileCleanup, AttachmentFileCleanup>()
            .AddScoped<IWorkItemBlockers, WorkItemBlockers>()
            .AddScoped<IWorkItemWhenEdits, WorkItemWhenEdits>()
            .AddScoped<IDependencyConflicts, DependencyConflicts>()
            .AddScoped<IWorkItemNameLookup, WorkItemNameLookup>()
            .AddScoped<IWorkItemHistoryService, WorkItemHistoryService>()
            .AddScoped<IWorkItemChangesService, WorkItemChangesService>()
            .AddScoped<IRunReportService, RunReportService>()
            .AddScoped<IWorkItemQueryService, WorkItemQueryService>()
            .AddScoped<ISpaceService, SpaceService>()
            .AddScoped<IViewService, ViewService>()
            .AddScoped<IStatusService, StatusService>()
            .AddScoped<IWorkItemTypeService, WorkItemTypeService>()
            .AddScoped<ICustomFieldService, CustomFieldService>()
            .AddScoped<IWorkItemRollUpService, WorkItemRollUpService>()
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
