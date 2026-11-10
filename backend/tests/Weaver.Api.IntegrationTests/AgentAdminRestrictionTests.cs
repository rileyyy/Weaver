using System.Net;
using Weaver.Api.Contracts;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// Agents can read admin data but never change it, whatever their key's scope (ADR-0004).
/// </summary>
[TestFixture]
public class AgentAdminRestrictionTests
{
    [Test]
    public async Task AReadWriteAgent_CantChangeAdminData()
    {
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);
        var space = await ApiClient.SharedSpaceIdAsync();

        var responses = new Dictionary<string, HttpResponseMessage>
        {
            ["time zone"] = await agent.PutJsonAsync("/api/server-settings/time-zone", new SetTimeZoneRequest("Europe/Berlin")),
            ["trash retention"] = await agent.PutJsonAsync("/api/server-settings/trash-retention", new SetTrashRetentionRequest(1)),
            ["done-hiding period"] = await agent.PutJsonAsync($"/api/spaces/{space}/done-hiding-period", new SetDoneHidingPeriodRequest(1)),
            ["status"] = await agent.PostJsonAsync("/api/statuses", new CreateStatusRequest(space, "Agent's", StatusCategory.ToDo, "#336699")),
            ["priority"] = await agent.PostJsonAsync("/api/work-item-priorities", new CreatePriorityRequest(space, "Agent's")),
            ["account"] = await agent.PostJsonAsync(
                "/api/admin/users",
                new CreateUserRequest(ApiClient.UniqueUsername(), ApiClient.Password, IsAdmin: true)),
            ["agent account"] = await agent.PostJsonAsync("/api/admin/agents", new CreateAgentAccountRequest("Another agent", null)),
        };

        Assert.Multiple(() =>
        {
            foreach (var (change, response) in responses)
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), change);
                Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"), change);
            }
        });
    }

    [Test]
    public async Task AnAgent_CantBeMadeAnAdmin()
    {
        var (agent, account) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);
        var admin = await ApiClient.FirstAdminAsync();

        var grant = await admin.PutJsonAsync($"/api/admin/users/{account.Id}/admin", new SetUserAdminRequest(true));
        var adminWrite = await agent.PostJsonAsync(
            "/api/work-item-priorities",
            new CreatePriorityRequest(await ApiClient.SharedSpaceIdAsync(), "Agent's"));
        var listed = await (await admin.GetAsync("/api/admin/users")).ReadAsync<List<AdminUserDto>>();

        Assert.Multiple(() =>
        {
            Assert.That(grant.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(adminWrite.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(listed.Single(u => u.Id == account.Id).IsAdmin, Is.False);
        });
    }
}
