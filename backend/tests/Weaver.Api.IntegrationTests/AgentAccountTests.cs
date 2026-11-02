using System.Net;
using Weaver.Api.Contracts;

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

    private static string UniqueAgentName() => $"Claude Code ({Guid.NewGuid():N})"[..40];
}
