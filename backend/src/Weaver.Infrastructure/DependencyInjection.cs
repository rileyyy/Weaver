using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Weaver.Application.Attachments;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Infrastructure.Attachments;
using Weaver.Infrastructure.Auth;

namespace Weaver.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWeaverInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<WeaverDbContext>((provider, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(new TimestampInterceptor(provider.GetRequiredService<TimeProvider>())));
        services.AddScoped<IWeaverDbContext>(provider => provider.GetRequiredService<WeaverDbContext>());
        services.AddScoped<IWorkItemHierarchy, PostgresWorkItemHierarchy>();
        services.AddSingleton<IWorkItemSearch, PostgresWorkItemSearch>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<FileSystemAttachmentStore>();
        services.AddSingleton<IAttachmentStore>(provider => provider.GetRequiredService<FileSystemAttachmentStore>());
        services.AddSingleton<IThumbnailGenerator, ImageSharpThumbnailGenerator>();
        return services;
    }
}
