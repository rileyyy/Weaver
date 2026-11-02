using System.Net;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Weaver.Api.Contracts;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// History over REST and MCP against Postgres: who is recorded (account, key, actor label),
/// that it's in the same transaction as the write, and that it outlives a delete.
/// </summary>
[TestFixture]
public class WorkItemHistoryTests
{
    [Test]
    public async Task RestWrites_RecordTheAccountAndTheActorLabelHeader_NewestFirst()
    {
        var (client, auth) = await ApiClient.SignedInAsync();
        var item = await client.CreateWorkItemAsync("Task");
        client.DefaultRequestHeaders.Add("X-Actor-Label", "script: nightly-tidy");

        (await client.PatchJsonAsync($"/api/work-items/{item.Id}/details", new { title = "Renamed" })).EnsureSuccessStatusCode();
        (await client.PutJsonAsync($"/api/work-items/{item.Id}/status", new ChangeWorkItemStatusRequest(SeededStatuses.Doing, null))).EnsureSuccessStatusCode();

        var history = await HistoryAsync(client, item.Id);

        Assert.Multiple(() =>
        {
            Assert.That(history.Select(e => e.Field), Is.EqualTo(new[] { "statusId", "title", "created" }));
            Assert.That(history.Select(e => e.AccountName), Is.All.EqualTo(auth.User.Username));
            Assert.That(history.Select(e => e.AccountKind), Is.All.EqualTo(UserKind.Human));
            Assert.That(history.Select(e => e.ApiKeyId), Is.All.Null);
            Assert.That(history[0].ActorLabel, Is.EqualTo("script: nightly-tidy"));
            Assert.That(history[0].NewName, Is.EqualTo("Doing"));
            Assert.That(history[1].OldValue?.GetString(), Is.EqualTo("Task"));
            Assert.That(history[1].NewValue?.GetString(), Is.EqualTo("Renamed"));
            Assert.That(history[2].ActorLabel, Is.Null);
        });
    }

    [Test]
    public async Task FailedWrite_RecordsNothing()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var item = await client.CreateWorkItemAsync("Task");

        var stale = await client.PatchJsonAsync(
            $"/api/work-items/{item.Id}/details", new { title = "Renamed", expectedVersion = item.Version + 1 });
        var invalid = await client.PatchJsonAsync($"/api/work-items/{item.Id}/details", new { title = " " });

        Assert.Multiple(async () =>
        {
            Assert.That(stale.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(invalid.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That((await HistoryAsync(client, item.Id)).Select(e => e.Field), Is.EqualTo(new[] { "created" }));
        });
    }

    [Test]
    public async Task ATooLongActorLabel_IsRejectedAndRecordsNothing()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var item = await client.CreateWorkItemAsync("Task");
        client.DefaultRequestHeaders.Add("X-Actor-Label", new string('x', HistoryActor.ActorLabelMaxLength + 1));

        var response = await client.PatchJsonAsync($"/api/work-items/{item.Id}/details", new { title = "Renamed" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await HistoryAsync(client, item.Id)).Select(e => e.Field), Is.EqualTo(new[] { "created" }));
    }

    [Test]
    public async Task Delete_IsRecorded_AndTheHistoryOutlivesTheItem()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var item = await client.CreateWorkItemAsync("Short-lived");

        (await client.DeleteAsync($"/api/work-items/{item.Id}")).EnsureSuccessStatusCode();

        var history = await HistoryAsync(client, item.Id);
        Assert.That(history.Select(e => (e.Field, e.OldValue?.GetString())), Is.EqualTo(new[]
        {
            ("deleted", (string?)"Short-lived"),
            ("created", null),
        }));
    }

    [Test]
    public async Task History_ForAnItemThatNeverExisted_Returns404()
    {
        var (client, _) = await ApiClient.SignedInAsync();

        var response = await client.GetAsync($"/api/work-items/{Guid.NewGuid()}/history");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task McpWrites_WithAnApiKey_RecordTheAgentItsKeyAndTheActorLabel()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var agentName = $"Agent ({Guid.NewGuid():N})"[..40];
        var agent = await (await admin.PostJsonAsync("/api/admin/agents", new CreateAgentAccountRequest(agentName, null)))
            .ReadAsync<AgentAccountDto>();
        var issued = await (await admin.PostJsonAsync(
                $"/api/admin/agents/{agent.Id}/keys",
                new CreateApiKeyRequest("laptop", ApiKeyScope.ReadWrite)))
            .ReadAsync<IssuedApiKeyDto>();
        var item = await admin.CreateWorkItemAsync("Task");
        await using var mcp = await ConnectAsync(ApiClient.WithApiKey(issued.Key));

        await CallAsync(mcp, "update_work_item_details", new()
        {
            ["id"] = item.Id,
            ["title"] = "Renamed by an agent",
            ["actorLabel"] = "sub-agent: test-writer",
        });
        await CallAsync(mcp, "add_comment", new() { ["workItemId"] = item.Id, ["body"] = "Done" });

        var history = await HistoryAsync(admin, item.Id);
        Assert.Multiple(() =>
        {
            Assert.That(history.Select(e => e.Field).Take(2), Is.EqualTo(new[] { "comment", "title" }));
            Assert.That(history.Take(2).Select(e => e.AccountId), Is.All.EqualTo(agent.Id));
            Assert.That(history.Take(2).Select(e => e.AccountName), Is.All.EqualTo(agentName));
            Assert.That(history.Take(2).Select(e => e.AccountKind), Is.All.EqualTo(UserKind.Agent));
            Assert.That(history.Take(2).Select(e => e.ApiKeyId), Is.All.EqualTo(issued.ApiKey.Id));
            Assert.That(history.Take(2).Select(e => e.ApiKeyLabel), Is.All.EqualTo("laptop"));
            Assert.That(history[1].ActorLabel, Is.EqualTo("sub-agent: test-writer"));
            Assert.That(history[0].ActorLabel, Is.Null);
        });
    }

    // Boards are views, not work items, so creating one records no history.
    [Test]
    public async Task EveryMcpWriteTool_TakesAnOptionalActorLabel()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        await using var mcp = await ConnectAsync(client);

        var tools = await mcp.ListToolsAsync();

        var writesWithoutLabel = tools
            .Where(t => t.ProtocolTool.Annotations?.ReadOnlyHint != true && t.Name != "create_board")
            .Where(t => !t.JsonSchema.GetProperty("properties").TryGetProperty("actorLabel", out _))
            .Select(t => t.Name);
        Assert.That(writesWithoutLabel, Is.Empty);
    }

    [Test]
    public async Task DeletingAnAgentWithHistory_IsRefused()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var agent = await (await admin.PostJsonAsync(
                "/api/admin/agents", new CreateAgentAccountRequest($"Agent ({Guid.NewGuid():N})"[..40], null)))
            .ReadAsync<AgentAccountDto>();
        var issued = await (await admin.PostJsonAsync(
                $"/api/admin/agents/{agent.Id}/keys",
                new CreateApiKeyRequest("laptop", ApiKeyScope.ReadWrite)))
            .ReadAsync<IssuedApiKeyDto>();
        await ApiClient.WithApiKey(issued.Key).CreateWorkItemAsync("Made by an agent");

        var response = await admin.DeleteAsync($"/api/admin/users/{agent.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    private static async Task<IReadOnlyList<WorkItemHistoryEntryDto>> HistoryAsync(HttpClient client, Guid workItemId) =>
        await (await client.GetAsync($"/api/work-items/{workItemId}/history"))
            .ReadAsync<IReadOnlyList<WorkItemHistoryEntryDto>>();

    private static async Task CallAsync(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, arguments);
        Assert.That(result.IsError, Is.Not.True, () => string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text)));
    }

    private static Task<McpClient> ConnectAsync(HttpClient client) =>
        McpClient.CreateAsync(new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(client.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            client,
            loggerFactory: null,
            ownsHttpClient: false));
}
