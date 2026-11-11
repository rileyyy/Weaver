using Microsoft.EntityFrameworkCore;
using Weaver.Application.Auth;
using Weaver.Application.Persistence;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

public class AgentAccountService(
    IWeaverDbContext db,
    IAdminAuthorizer admins,
    ICallerContext caller,
    IUserAdministrationService users,
    TimeProvider clock) : IAgentAccountService
{
    private readonly IWeaverDbContext _db = db;
    private readonly IAdminAuthorizer _admins = admins;
    private readonly ICallerContext _caller = caller;
    private readonly IUserAdministrationService _users = users;
    private readonly TimeProvider _clock = clock;

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct = default)
    {
        await EnsureAdminAsync(ct);
        return await _db.Users.AsNoTracking()
            .Where(u => u.Kind == UserKind.Agent)
            .OrderBy(u => u.NormalizedUsername)
            .ToListAsync(ct);
    }

    public async Task<User> CreateAsync(string name, string? description, string? handle, CancellationToken ct = default)
    {
        await EnsureAdminAsync(ct);

        var agent = User.CreateAgent(name, description);
        agent.SetHandle(handle);
        // Agents share the username index with humans, so a name can't belong to both.
        if (await _db.Users.AnyAsync(u => u.NormalizedUsername == agent.NormalizedUsername, ct))
        {
            throw new UsernameTakenException(agent.Username);
        }

        await EnsureHandleFreeAsync(agent, ct);

        _db.Users.Add(agent);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException ex)
        {
            throw await RaceLostExceptionAsync(agent, ex, ct);
        }

        return agent;
    }

    public async Task<User> SetHandleAsync(Guid agentId, string? handle, CancellationToken ct = default)
    {
        await EnsureAdminAsync(ct);
        var agent = await FindAgentAsync(agentId, ct);

        agent.SetHandle(handle);
        await EnsureHandleFreeAsync(agent, ct);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException ex)
        {
            throw new HandleTakenException(agent.Handle!, ex);
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

    private async Task EnsureHandleFreeAsync(User account, CancellationToken ct)
    {
        if (account.Handle is { } handle && await IsHandleTakenAsync(account, handle, ct))
        {
            throw new HandleTakenException(handle);
        }
    }

    private Task<bool> IsHandleTakenAsync(User account, string handle, CancellationToken ct) =>
        _db.Users.AnyAsync(u => u.Handle == handle && u.Id != account.Id, ct);

    /// <summary>A save that lost a race names whichever unique value the other save took first.</summary>
    private async Task<DomainException> RaceLostExceptionAsync(User agent, Exception ex, CancellationToken ct) =>
        agent.Handle is { } handle && await IsHandleTakenAsync(agent, handle, ct)
            ? new HandleTakenException(handle, ex)
            : new UsernameTakenException(agent.Username, ex);

    private async Task<User> FindAgentAsync(Guid agentId, CancellationToken ct)
    {
        var account = await _db.Users.FindAsync([agentId], ct)
            ?? throw new EntityNotFoundException("Agent account", agentId);
        return account.Kind == UserKind.Agent ? account : throw new NotAnAgentAccountException(account.Username);
    }

    private Task EnsureAdminAsync(CancellationToken ct) => _admins.EnsureAdminAsync(_caller.Current.AccountId, ct);
}
