using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;

namespace Weaver.Application.Services;

public class FirstAdminBootstrapper : IFirstAdminBootstrapper
{
    private readonly IWeaverDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly FirstAdminOptions _options;

    public FirstAdminBootstrapper(
        IWeaverDbContext db,
        IPasswordHasher<User> passwordHasher,
        IOptions<FirstAdminOptions> options)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _options = options.Value;
    }

    public async Task<FirstAdminBootstrapResult> EnsureFirstAdminAsync(CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(ct))
        {
            return FirstAdminBootstrapResult.AccountsAlreadyExist;
        }

        if (string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrEmpty(_options.Password))
        {
            return FirstAdminBootstrapResult.NotConfigured;
        }

        var (trimmed, normalized) = UserCredentials.NormalizeUsername(_options.Username);
        UserCredentials.ValidatePassword(_options.Password);
        var user = User.CreateHuman(trimmed, normalized, isAdmin: true);
        user.SetPasswordHash(_passwordHasher.HashPassword(user, _options.Password));
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return FirstAdminBootstrapResult.Created;
    }
}
