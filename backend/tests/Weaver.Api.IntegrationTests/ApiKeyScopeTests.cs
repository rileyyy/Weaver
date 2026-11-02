using System.Net;
using Weaver.Api.Contracts;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// What an agent's key lets it do over REST (ADR-0004): a read-only key reads but never writes,
/// and a read-write key writes like a non-admin human.
/// </summary>
[TestFixture]
public class ApiKeyScopeTests
{
    [Test]
    public async Task AReadOnlyKey_CantCreateAWorkItem()
    {
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);

        var response = await agent.PostJsonAsync(
            "/api/work-items",
            new CreateWorkItemRequest("Not allowed", null, null, SeededStatuses.ToDo, null));

        Assert.Multiple(async () =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
            Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("read-only"));
        });
    }

    [Test]
    public async Task AReadOnlyKey_CantChangeOrDeleteAnExistingWorkItem()
    {
        var (human, _) = await ApiClient.SignedInAsync();
        var item = await human.CreateWorkItemAsync("Leave me alone");
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);

        var move = await agent.PutJsonAsync(
            $"/api/work-items/{item.Id}/status",
            new ChangeWorkItemStatusRequest(SeededStatuses.Doing, null));
        var delete = await agent.DeleteAsync($"/api/work-items/{item.Id}");
        var after = await (await human.GetAsync($"/api/work-items/{item.Id}")).ReadAsync<WorkItemDto>();

        Assert.Multiple(() =>
        {
            Assert.That(move.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(after.StatusId, Is.EqualTo(SeededStatuses.ToDo));
        });
    }

    [Test]
    public async Task AReadOnlyKey_CanStillRead()
    {
        var (human, _) = await ApiClient.SignedInAsync();
        var item = await human.CreateWorkItemAsync("Readable");
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);

        var read = await agent.GetAsync($"/api/work-items/{item.Id}");
        var settings = await agent.GetAsync("/api/server-settings");

        Assert.Multiple(() =>
        {
            Assert.That(read.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(settings.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task AReadWriteKey_CanCreateChangeAndDeleteWorkItems()
    {
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);

        var item = await agent.CreateWorkItemAsync("Written by an agent");
        var moved = await (await agent.PutJsonAsync(
                $"/api/work-items/{item.Id}/status",
                new ChangeWorkItemStatusRequest(SeededStatuses.Doing, null)))
            .ReadAsync<WorkItemDto>();
        var comment = await agent.PostJsonAsync($"/api/work-items/{item.Id}/comments", new CreateCommentRequest("Started"));
        var delete = await agent.DeleteAsync($"/api/work-items/{item.Id}");

        Assert.Multiple(() =>
        {
            Assert.That(moved.StatusId, Is.EqualTo(SeededStatuses.Doing));
            Assert.That(comment.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        });
    }
}
