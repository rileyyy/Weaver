using System.Net;
using Weaver.Api.Contracts;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

/// <summary>Agent accounts and their API keys (ADR-0004), managed by an admin over REST.</summary>
[TestFixture]
public class AgentAccountTests
{
    [Test]
    public async Task AnAdmin_CanCreateAnAgentAccount_AndSeeItListed()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var name = UniqueAgentName();

        var response = await admin.PostJsonAsync(
            "/api/admin/agents",
            new CreateAgentAccountRequest(name, "Runs on the desktop"));
        var created = await response.ReadAsync<AgentAccountDto>();
        var listed = await (await admin.GetAsync("/api/admin/agents")).ReadAsync<List<AgentAccountDto>>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(created.Name, Is.EqualTo(name));
            Assert.That(created.Description, Is.EqualTo("Runs on the desktop"));
            Assert.That(created.IsActive, Is.True);
            Assert.That(listed.Select(a => a.Id), Does.Contain(created.Id));
        });
    }

    [Test]
    public async Task AgentAccounts_AreNotManageableByANonAdmin()
    {
        var (member, _) = await ApiClient.SignedInAsync();

        var create = await member.PostJsonAsync("/api/admin/agents", new CreateAgentAccountRequest(UniqueAgentName(), null));
        var list = await member.GetAsync("/api/admin/agents");

        Assert.Multiple(() =>
        {
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });
    }

    [Test]
    public async Task IssuingAKey_ShowsItOnce_AndTheListNeverShowsItAgain()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var agent = await CreateAgentAsync(admin);

        var response = await admin.PostJsonAsync(
            $"/api/admin/agents/{agent.Id}/keys",
            new CreateApiKeyRequest("desktop", ApiKeyScope.ReadWrite));
        var issued = await response.ReadAsync<IssuedApiKeyDto>();
        var listResponse = await admin.GetAsync($"/api/admin/agents/{agent.Id}/keys");
        var listJson = await listResponse.Content.ReadAsStringAsync();
        var listed = await listResponse.ReadAsync<List<ApiKeyDto>>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(issued.Key, Does.StartWith("wvr_").And.Length.GreaterThan(40));
            Assert.That(listed, Has.Count.EqualTo(1));
            Assert.That(listed[0].Id, Is.EqualTo(issued.ApiKey.Id));
            Assert.That(listed[0].Label, Is.EqualTo("desktop"));
            Assert.That(listed[0].Scope, Is.EqualTo(ApiKeyScope.ReadWrite));
            Assert.That(listed[0].CreatedAtUtc, Is.Not.EqualTo(default(DateTimeOffset)));
            Assert.That(listed[0].LastUsedAtUtc, Is.Null);
            Assert.That(listJson, Does.Not.Contain(issued.Key[4..]));
        });
    }

    [Test]
    public async Task AHumanAccount_CantHaveKeys()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var me = await (await admin.GetAsync("/api/auth/me")).ReadAsync<UserDto>();

        var response = await admin.PostJsonAsync(
            $"/api/admin/agents/{me.Id}/keys",
            new CreateApiKeyRequest("desktop", ApiKeyScope.ReadOnly));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task AKey_SignsInAsItsAgentAccount_AndRecordsItsUse()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var agent = await CreateAgentAsync(admin);
        var issued = await IssueKeyAsync(admin, agent.Id);

        var me = await (await ApiClient.WithApiKey(issued.Key).GetAsync("/api/auth/me")).ReadAsync<UserDto>();
        var keys = await (await admin.GetAsync($"/api/admin/agents/{agent.Id}/keys")).ReadAsync<List<ApiKeyDto>>();

        Assert.Multiple(() =>
        {
            Assert.That(me.Id, Is.EqualTo(agent.Id));
            Assert.That(me.Kind, Is.EqualTo(UserKind.Agent));
            Assert.That(keys.Single().LastUsedAtUtc, Is.Not.Null);
        });
    }

    [Test]
    public async Task AnUnknownKey_Gets401()
    {
        var response = await ApiClient.WithApiKey("wvr_not-a-key-anyone-was-issued").GetAsync("/api/auth/me");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ARevokedKey_Gets401_WhileTheAgentsOtherKeysStillWork()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var agent = await CreateAgentAsync(admin);
        var revoked = await IssueKeyAsync(admin, agent.Id);
        var kept = await IssueKeyAsync(admin, agent.Id);

        var revoke = await admin.DeleteAsync($"/api/admin/agents/{agent.Id}/keys/{revoked.ApiKey.Id}");
        var withRevoked = await ApiClient.WithApiKey(revoked.Key).GetAsync("/api/auth/me");
        var withKept = await ApiClient.WithApiKey(kept.Key).GetAsync("/api/auth/me");
        var keys = await (await admin.GetAsync($"/api/admin/agents/{agent.Id}/keys")).ReadAsync<List<ApiKeyDto>>();

        Assert.Multiple(() =>
        {
            Assert.That(revoke.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(withRevoked.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(withKept.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(keys.Single(k => k.Id == revoked.ApiKey.Id).RevokedAtUtc, Is.Not.Null);
        });
    }

    [Test]
    public async Task ADeactivatedAgent_GetsA401ForEveryKey_UntilReactivated()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var agent = await CreateAgentAsync(admin);
        var issued = await IssueKeyAsync(admin, agent.Id);

        var deactivated = await (await admin.PutJsonAsync($"/api/admin/agents/{agent.Id}/active", new SetUserActiveRequest(false)))
            .ReadAsync<AgentAccountDto>();
        var whileDeactivated = await ApiClient.WithApiKey(issued.Key).GetAsync("/api/auth/me");
        await admin.PutJsonAsync($"/api/admin/agents/{agent.Id}/active", new SetUserActiveRequest(true));
        var afterReactivation = await ApiClient.WithApiKey(issued.Key).GetAsync("/api/auth/me");

        Assert.Multiple(() =>
        {
            Assert.That(deactivated.IsActive, Is.False);
            Assert.That(whileDeactivated.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(afterReactivation.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task AnAgentAccount_CantBeGivenAPassword_OrSignInWithOne()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var name = $"nightly-{Guid.NewGuid():N}"[..30];
        var agent = await (await admin.PostJsonAsync("/api/admin/agents", new CreateAgentAccountRequest(name, null)))
            .ReadAsync<AgentAccountDto>();

        // The generic account endpoints accept an agent's id, but not a password for it.
        var reset = await admin.PutJsonAsync($"/api/admin/users/{agent.Id}/password", new ResetPasswordRequest(ApiClient.Password));
        var login = await ApiClient.Anonymous().PostJsonAsync("/api/auth/login", new LoginRequest(name, ApiClient.Password));

        Assert.Multiple(() =>
        {
            Assert.That(reset.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        });
    }

    private static async Task<IssuedApiKeyDto> IssueKeyAsync(HttpClient admin, Guid agentId) =>
        await (await admin.PostJsonAsync(
                $"/api/admin/agents/{agentId}/keys",
                new CreateApiKeyRequest("desktop", ApiKeyScope.ReadWrite)))
            .ReadAsync<IssuedApiKeyDto>();

    private static async Task<AgentAccountDto> CreateAgentAsync(HttpClient admin) =>
        await (await admin.PostJsonAsync("/api/admin/agents", new CreateAgentAccountRequest(UniqueAgentName(), null)))
            .ReadAsync<AgentAccountDto>();

    private static string UniqueAgentName() => $"Claude Code ({Guid.NewGuid():N})"[..40];
}
