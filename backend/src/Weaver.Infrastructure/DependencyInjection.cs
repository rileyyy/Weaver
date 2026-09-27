using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Infrastructure.Auth;

namespace Weaver.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWeaverInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<WeaverDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IWeaverDbContext>(provider => provider.GetRequiredService<WeaverDbContext>());
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        return services;
    }
}
