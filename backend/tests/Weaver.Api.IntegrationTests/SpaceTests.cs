using System.Net;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Weaver.Api.Contracts;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

/// <summary>Spaces over REST and MCP, against Postgres: membership, the reparent boundary and who may change them.</summary>
[TestFixture]
public class SpaceTests
{
    [Test]
    public async Task Reparent_UnderAnItemInAnotherSpace_Returns409AndChangesNothing()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var home = await ApiClient.CreateSpaceAsync("Home");
        var village = await ApiClient.CreateSpaceAsync("Village");
        var kitchen = await client.CreateWorkItemAsync("Kitchen", spaceId: home);
        var fence = await client.CreateWorkItemAsync("Fence", spaceId: village);

        var response = await client.PutJsonAsync(
            $"/api/work-items/{kitchen.Id}/parent",
            new ReparentWorkItemRequest(fence.Id, null));

        var after = await (await client.GetAsync($"/api/work-items/{kitchen.Id}")).ReadAsync<WorkItemDto>();
        var history = await (await client.GetAsync($"/api/work-items/{kitchen.Id}/history"))
            .ReadAsync<IReadOnlyList<WorkItemHistoryEntryDto>>();
        Assert.Multiple(async () =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("another Space"));
            Assert.That(after.ParentId, Is.Null);
            Assert.That(after.SpaceId, Is.EqualTo(home));
            Assert.That(after.Version, Is.EqualTo(kitchen.Version));
            Assert.That(history.Select(e => e.Field), Does.Not.Contain(WorkItemField.ParentId));
        });
    }

    [Test]
    public async Task Reparent_OverMcp_UnderAnItemInAnotherSpace_IsAnError()
    {
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);
        var kitchen = await agent.CreateWorkItemAsync("Kitchen", spaceId: await ApiClient.CreateSpaceAsync("Home"));
        var fence = await agent.CreateWorkItemAsync("Fence", spaceId: await ApiClient.CreateSpaceAsync("Village"));
        await using var mcp = await ConnectAsync(agent);

        var result = await mcp.CallToolAsync("reparent_work_item", new Dictionary<string, object?>
        {
            ["id"] = kitchen.Id,
            ["parentId"] = fence.Id,
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.True);
            Assert.That(TextOf(result), Does.Contain("another Space"));
        });
    }

    [Test]
    public async Task Create_AChild_JoinsItsParentsSpace_AndTopLevelListingsStayInTheirSpace()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var home = await ApiClient.CreateSpaceAsync("Home");
        var village = await ApiClient.CreateSpaceAsync("Village");
        var kitchen = await client.CreateWorkItemAsync("Kitchen", spaceId: home);
        var sink = await client.CreateWorkItemAsync("Sink", kitchen.Id);
        await client.CreateWorkItemAsync("Fence", spaceId: village);

        var topLevel = await (await client.GetAsync($"/api/work-items?spaceId={home}")).ReadAsync<List<WorkItemDto>>();
        var lanes = await (await client.GetAsync($"/api/work-items/swimlanes?spaceId={home}")).ReadAsync<List<SwimlaneDto>>();
        var all = await (await client.GetAsync($"/api/work-items/all?spaceId={home}")).ReadAsync<List<WorkItemDto>>();
        var unscoped = await client.GetAsync("/api/work-items");

        Assert.Multiple(() =>
        {
            Assert.That(sink.SpaceId, Is.EqualTo(home));
            Assert.That(topLevel.Select(w => w.Id), Is.EqualTo(new[] { kitchen.Id }));
            Assert.That(lanes.Single().Cards.Select(c => c.Id), Is.EqualTo(new[] { sink.Id }));
            Assert.That(all.Select(w => w.Id), Is.EquivalentTo(new[] { kitchen.Id, sink.Id }));
            Assert.That(unscoped.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task Create_ATopLevelItemWithoutASpace_Returns400()
    {
        var (client, _) = await ApiClient.SignedInAsync();

        var response = await client.PostJsonAsync(
            "/api/work-items",
            new CreateWorkItemRequest(
                "Nowhere", null, null, await ApiClient.StatusIdAsync(await ApiClient.SharedSpaceIdAsync(), "To Do"), null));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Create_RecordsTheItemsSpaceInHistory_ByName()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var space = await (await admin.PostJsonAsync("/api/spaces", new SpaceNameRequest($"Garden {Guid.NewGuid():N}"[..30])))
            .ReadAsync<SpaceDto>();
        var item = await client.CreateWorkItemAsync("Plant bulbs", spaceId: space.Id);

        var history = await (await client.GetAsync($"/api/work-items/{item.Id}/history"))
            .ReadAsync<IReadOnlyList<WorkItemHistoryEntryDto>>();

        var entry = history.Single(e => e.Field == WorkItemField.SpaceId);
        Assert.Multiple(() =>
        {
            Assert.That(entry.NewValue?.GetGuid(), Is.EqualTo(space.Id));
            Assert.That(entry.NewName, Is.EqualTo(space.Name));
        });
    }

    [Test]
    public async Task AnAdmin_CreatesAndRenamesASpace_WhichEveryoneCanList()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var (member, _) = await ApiClient.SignedInAsync();
        var name = $"Software {Guid.NewGuid():N}"[..30];

        var created = await admin.PostJsonAsync("/api/spaces", new SpaceNameRequest(name));
        var space = await created.ReadAsync<SpaceDto>();
        var renamed = await (await admin.PutJsonAsync($"/api/spaces/{space.Id}", new SpaceNameRequest(name + " 2")))
            .ReadAsync<SpaceDto>();
        var listed = await (await member.GetAsync("/api/spaces")).ReadAsync<List<SpaceDto>>();
        var duplicate = await admin.PostJsonAsync("/api/spaces", new SpaceNameRequest(name.ToUpperInvariant() + " 2"));

        Assert.Multiple(() =>
        {
            Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(renamed.Name, Is.EqualTo(name + " 2"));
            Assert.That(listed, Does.Contain(renamed));
            Assert.That(duplicate.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        });
    }

    [Test]
    public async Task ANonAdminOrAnAgent_CantCreateOrRenameASpace()
    {
        var (member, _) = await ApiClient.SignedInAsync();
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);
        var spaceId = await ApiClient.SharedSpaceIdAsync();

        var byMember = await member.PostJsonAsync("/api/spaces", new SpaceNameRequest("Not allowed"));
        var byAgent = await agent.PostJsonAsync("/api/spaces", new SpaceNameRequest("Not allowed"));
        var renameByAgent = await agent.PutJsonAsync($"/api/spaces/{spaceId}", new SpaceNameRequest("Not allowed"));
        var listByAgent = await agent.GetAsync("/api/spaces");

        Assert.Multiple(() =>
        {
            Assert.That(byMember.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(byAgent.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(renameByAgent.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(listByAgent.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task AnAdmin_SetsTheDoneHidingPeriod_WhichAgentsCanRead_ButNoOneElseCanChange()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var (member, _) = await ApiClient.SignedInAsync();
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);
        var space = await (await admin.PostJsonAsync("/api/spaces", new SpaceNameRequest($"Hiding {Guid.NewGuid():N}"[..30])))
            .ReadAsync<SpaceDto>();

        var set = await admin.PutJsonAsync($"/api/spaces/{space.Id}/done-hiding-period", new SetDoneHidingPeriodRequest(30));
        var outOfRange = await admin.PutJsonAsync($"/api/spaces/{space.Id}/done-hiding-period", new SetDoneHidingPeriodRequest(0));
        var byMember = await member.PutJsonAsync($"/api/spaces/{space.Id}/done-hiding-period", new SetDoneHidingPeriodRequest(7));
        var byAgent = await agent.PutJsonAsync($"/api/spaces/{space.Id}/done-hiding-period", new SetDoneHidingPeriodRequest(7));
        await using var mcp = await ConnectAsync(agent);
        var listed = JsonSerializer.Deserialize<List<SpaceDto>>(
            TextOf(await mcp.CallToolAsync("list_spaces", new Dictionary<string, object?>())), ApiClient.Json)!;

        Assert.Multiple(async () =>
        {
            Assert.That(space.DoneHidingDays, Is.EqualTo(14));
            Assert.That(set.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That((await set.ReadAsync<SpaceDto>()).DoneHidingDays, Is.EqualTo(30));
            Assert.That(outOfRange.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(byMember.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(byAgent.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(listed.Single(s => s.Id == space.Id).DoneHidingDays, Is.EqualTo(30));
        });
    }

    [Test]
    public async Task Mcp_ListsSpaces_ButOffersNoToolToChangeThem()
    {
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);
        var spaceId = await ApiClient.SharedSpaceIdAsync();
        await using var mcp = await ConnectAsync(agent);

        var tools = await mcp.ListToolsAsync();
        var listed = await mcp.CallToolAsync("list_spaces", new Dictionary<string, object?>());

        var spaces = JsonSerializer.Deserialize<List<SpaceDto>>(TextOf(listed), ApiClient.Json)!;
        Assert.Multiple(() =>
        {
            Assert.That(spaces.Select(s => s.Id), Does.Contain(spaceId));
            // Moving a work item into another Space writes the item, not the Space.
            Assert.That(
                tools.Where(t => t.Name.Contains("space") && !t.Name.Contains("work_item"))
                    .Select(t => t.ProtocolTool.Annotations?.ReadOnlyHint),
                Has.All.True);
        });
    }

    private static string TextOf(CallToolResult result) =>
        string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

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
