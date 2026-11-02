using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class AgentAccountService : IAgentAccountService
{
    private readonly IWeaverDbContext _db;
    private readonly IAdminAuthorizer _admins;
    private readonly ICallerContext _caller;
    private readonly IUserAdministrationService _users;
    private readonly TimeProvider _clock;

    public AgentAccountService(
        IWeaverDbContext db,
        IAdminAuthorizer admins,
        ICallerContext caller,
        IUserAdministrationService users,
        TimeProvider clock)
    {
        _db = db;
        _admins = admins;
        _caller = caller;
        _users = users;
        _clock = clock;
    }

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct = default)
    {
        await EnsureAdminAsync(ct);
        return await _db.Users.AsNoTracking()
            .Where(u => u.Kind == UserKind.Agent)
            .OrderBy(u => u.NormalizedUsername)
            .ToListAsync(ct);
    }

    public async Task<User> CreateAsync(string name, string? description, CancellationToken ct = default)
    {
        await EnsureAdminAsync(ct);

        var agent = User.CreateAgent(name, description);
        // Agents share the username index with humans, so a name can't belong to both.
        if (await _db.Users.AnyAsync(u => u.NormalizedUsername == agent.NormalizedUsername, ct))
        {
            throw new UsernameTakenException(agent.Username);
        }

        _db.Users.Add(agent);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException ex)
        {
            throw new UsernameTakenException(agent.Username, ex);
        }

        return agent;
    }

    public async Task<User> SetActiveAsync(Guid agentId, bool isActive, CancellationToken ct = default)
    {
        await EnsureAdminAsync(ct);
        await FindAgentAsync(agentId, ct);

        // Deactivation's other rules (unassigning work, ending sessions) are the same as a human's.
        return await _users.SetActiveAsync(_caller.Current.AccountId, agentId, isActive, ct);
    }

    public async Task<IReadOnlyList<ApiKey>> ListKeysAsync(Guid agentId, CancellationToken ct = default)
    {
        await EnsureAdminAsync(ct);
        await FindAgentAsync(agentId, ct);

        return await _db.ApiKeys.AsNoTracking()
            .Where(k => k.UserId == agentId)
            .OrderBy(k => k.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<IssuedApiKey> IssueKeyAsync(
        Guid agentId,
        string label,
        ApiKeyScope scope,
        CancellationToken ct = default)
    {
        await EnsureAdminAsync(ct);
        var agent = await FindAgentAsync(agentId, ct);

        var key = ApiKeySecret.Generate();
        var apiKey = ApiKey.Issue(agent, label, scope, ApiKeySecret.Hash(key));
        _db.ApiKeys.Add(apiKey);
        await _db.SaveChangesAsync(ct);

        return new IssuedApiKey(apiKey, key);
    }

    public async Task RevokeKeyAsync(Guid agentId, Guid keyId, CancellationToken ct = default)
    {
        await EnsureAdminAsync(ct);
        await FindAgentAsync(agentId, ct);

        var apiKey = await _db.ApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == agentId, ct)
            ?? throw new EntityNotFoundException(nameof(ApiKey), keyId);
        apiKey.Revoke(_clock.GetUtcNow());

        await _db.SaveChangesAsync(ct);
    }

    private async Task<User> FindAgentAsync(Guid agentId, CancellationToken ct)
    {
        var account = await _db.Users.FindAsync([agentId], ct)
            ?? throw new EntityNotFoundException("Agent account", agentId);
        return account.Kind == UserKind.Agent ? account : throw new NotAnAgentAccountException(account.Username);
    }

    private Task EnsureAdminAsync(CancellationToken ct) => _admins.EnsureAdminAsync(_caller.Current.AccountId, ct);
}
