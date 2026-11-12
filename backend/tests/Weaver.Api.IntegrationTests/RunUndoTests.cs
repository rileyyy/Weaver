using System.Net;
using Weaver.Api.Contracts;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// Undoing a run against Postgres, where the undo's writes share one transaction: an agent's
/// dates and link are removed, a field a human changed since is kept, and only admins may undo.
/// </summary>
[TestFixture]
public class RunUndoTests
{
    [Test]
    public async Task AnAdmin_UndoesARun_KeepingWhatAHumanChangedSince()
    {
        var admin = await ApiClient.FirstAdminAsync();
        var (agentClient, agent) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);
        var drywall = await admin.CreateWorkItemAsync("Drywall");
        var paint = await admin.CreateWorkItemAsync("Paint the hall");
        var runId = Guid.NewGuid().ToString();
        agentClient.DefaultRequestHeaders.Add("X-Agent-Run", runId);
        (await agentClient.PatchJsonAsync($"/api/work-items/{paint.Id}/schedule", new { startDate = "2027-03-20", endDate = "2027-03-21" }))
            .EnsureSuccessStatusCode();
        (await agentClient.PatchJsonAsync($"/api/work-items/{paint.Id}/details", new { when = "after drywall" }))
            .EnsureSuccessStatusCode();
        (await agentClient.PostJsonAsync(
                $"/api/work-items/{paint.Id}/links", new CreateWorkItemLinkRequest(drywall.Id, WorkItemLinkRelation.BlockedBy)))
            .EnsureSuccessStatusCode();
        (await admin.PatchJsonAsync($"/api/work-items/{paint.Id}/details", new { when = "after the drywall dries" }))
            .EnsureSuccessStatusCode();
        var page = await (await admin.GetAsync("/api/admin/runs?limit=200")).ReadAsync<RunReportPageDto>();
        var run = page.Runs.Single(r => r.RunId == runId);

        var response = await admin.PostAsync(UndoPath(run.AccountId, run.ApiKeyId, runId), null);

        var undo = await response.ReadAsync<RunUndoResultDto>();
        var item = await (await admin.GetAsync($"/api/work-items/{paint.Id}")).ReadAsync<WorkItemDto>();
        var links = await (await admin.GetAsync($"/api/work-items/{paint.Id}/links")).ReadAsync<List<WorkItemLinkDto>>();
        Assert.Multiple(() =>
        {
            Assert.That(run.AccountId, Is.EqualTo(agent.Id));
            Assert.That(item.StartDate, Is.Null);
            Assert.That(item.EndDate, Is.Null);
            Assert.That(item.When, Is.EqualTo("after the drywall dries"));
            Assert.That(links, Is.Empty);
            Assert.That(undo.Changes.Single(c => c.Field == "when").Outcome, Is.EqualTo(RunUndoOutcome.ChangedSince));
            Assert.That(undo.Changes.Count(c => c.Outcome == RunUndoOutcome.Undone), Is.EqualTo(4));
        });
    }

    [Test]
    public async Task UndoingARunThatDoesNotExist_Returns404()
    {
        var admin = await ApiClient.FirstAdminAsync();

        var response = await admin.PostAsync(UndoPath(Guid.NewGuid(), Guid.NewGuid(), "nothing"), null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Undo_IsRefusedToNonAdminsAndAgents()
    {
        var (human, _) = await ApiClient.SignedInAsync();
        var (agentClient, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);
        var path = UndoPath(Guid.NewGuid(), Guid.NewGuid(), "run-1");

        var fromHuman = await human.PostAsync(path, null);
        var fromAgent = await agentClient.PostAsync(path, null);

        Assert.Multiple(() =>
        {
            Assert.That(fromHuman.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(fromAgent.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });
    }

    private static string UndoPath(Guid accountId, Guid apiKeyId, string runId) =>
        $"/api/admin/runs/undo?accountId={accountId}&apiKeyId={apiKeyId}&runId={Uri.EscapeDataString(runId)}";
}
