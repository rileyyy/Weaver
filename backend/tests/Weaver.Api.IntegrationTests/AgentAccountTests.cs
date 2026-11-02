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

    private static async Task<AgentAccountDto> CreateAgentAsync(HttpClient admin) =>
        await (await admin.PostJsonAsync("/api/admin/agents", new CreateAgentAccountRequest(UniqueAgentName(), null)))
            .ReadAsync<AgentAccountDto>();

    private static string UniqueAgentName() => $"Claude Code ({Guid.NewGuid():N})"[..40];
}
