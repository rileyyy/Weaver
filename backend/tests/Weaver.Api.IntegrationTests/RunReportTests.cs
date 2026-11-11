using System.Net;
using Weaver.Api.Contracts;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// Run reports against Postgres: an agent's writes sent with a run id are listed as one run and
/// read back as that run's changes, and only admins may read them.
/// </summary>
[TestFixture]
public class RunReportTests
{
    [Test]
    public async Task AnAgentsRun_IsListedForAdmins_AndItsChangesReadBack()
    {
        var admin = await ApiClient.FirstAdminAsync();
        var (agentClient, agent) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);
        var hall = await admin.CreateWorkItemAsync("Paint the hall");
        var shed = await admin.CreateWorkItemAsync("Fix the shed");
        var runId = Guid.NewGuid().ToString();
        agentClient.DefaultRequestHeaders.Add("X-Agent-Run", runId);

        (await agentClient.PatchJsonAsync($"/api/work-items/{shed.Id}/details", new { when = "next week" })).EnsureSuccessStatusCode();
        (await agentClient.PatchJsonAsync($"/api/work-items/{hall.Id}/details", new { title = "Paint the hallway" })).EnsureSuccessStatusCode();

        var page = await (await admin.GetAsync($"/api/admin/runs?limit={200}")).ReadAsync<RunReportPageDto>();
        var run = page.Runs.Single(r => r.RunId == runId);
        var changes = await (await admin.GetAsync(
                $"/api/admin/runs/changes?accountId={run.AccountId}&apiKeyId={run.ApiKeyId}&runId={Uri.EscapeDataString(runId)}"))
            .ReadAsync<IReadOnlyList<ChangedWorkItemDto>>();

        Assert.Multiple(() =>
        {
            Assert.That(run.AccountId, Is.EqualTo(agent.Id));
            Assert.That(run.AccountName, Is.EqualTo(agent.Name));
            Assert.That(run.ApiKeyLabel, Is.EqualTo("test"));
            Assert.That(run.ChangeCount, Is.EqualTo(2));
            Assert.That(run.FirstChangeAtUtc, Is.LessThanOrEqualTo(run.LastChangeAtUtc));
            Assert.That(changes.Select(c => c.Item.Title), Is.EqualTo(new[] { "Fix the shed", "Paint the hallway" }));
            Assert.That(changes.Select(c => c.Changes.Single().Field), Is.EqualTo(new[] { "when", "title" }));
            Assert.That(changes.SelectMany(c => c.Changes).Select(e => e.RunId), Is.All.EqualTo(runId));
        });
    }

    [Test]
    public async Task ARunThatDoesNotExist_Returns404()
    {
        var admin = await ApiClient.FirstAdminAsync();

        var response = await admin.GetAsync($"/api/admin/runs/changes?accountId={Guid.NewGuid()}&runId=nothing");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task RunReports_AreRefusedToNonAdminsAndAgents()
    {
        var (human, _) = await ApiClient.SignedInAsync();
        var (agentClient, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);

        var fromHuman = await human.GetAsync("/api/admin/runs");
        var fromAgent = await agentClient.GetAsync("/api/admin/runs");

        Assert.Multiple(() =>
        {
            Assert.That(fromHuman.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(fromAgent.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });
    }
}
