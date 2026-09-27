using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
            .AddScoped<ICommentService, CommentService>()
            .AddScoped<IWorkItemLinkService, WorkItemLinkService>()
            .AddScoped<IBoardService, BoardService>()
            .AddScoped<IStatusService, StatusService>()
            .AddScoped<IWorkItemLayerService, WorkItemLayerService>()
            .AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>()
            .AddTimeProvider();

    private static IServiceCollection AddTimeProvider(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
