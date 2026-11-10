using System.Net;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Weaver.Api.Contracts;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// Moving a work item between Spaces against Postgres (ticket 16): the recursive subtree walk
/// with the Trash included, the one-save move, links across Spaces, history, and key scopes over
/// REST and MCP.
/// </summary>
[TestFixture]
public class SpaceMoveTests
{
    private HttpClient _client = null!;
    private Guid _home;
    private Guid _workshop;

    [SetUp]
    public async Task SetUp()
    {
        (_client, _) = await ApiClient.SignedInAsync();
        _home = await ApiClient.CreateSpaceAsync("Move from");
        _workshop = await ApiClient.CreateSpaceAsync("Move to");
    }

    private Task<HttpResponseMessage> MoveAsync(HttpClient client, Guid id, object body) =>
        client.PostJsonAsync($"/api/work-items/{id}/space-move", body);

    private async Task<WorkItemDto> GetAsync(Guid id) =>
        await (await _client.GetAsync($"/api/work-items/{id}")).ReadAsync<WorkItemDto>();

    [Test]
    public async Task Move_TakesTheWholeSubtree_IncludingASubItemInTheTrash_AndKeepsLinks()
    {
        var shed = await _client.CreateWorkItemAsync("Shed", spaceId: _home);
        var roof = await _client.CreateWorkItemAsync("Roof", shed.Id);
        var tiles = await _client.CreateWorkItemAsync("Tiles", roof.Id);
        var door = await _client.CreateWorkItemAsync("Door", shed.Id);
        var kitchen = await _client.CreateWorkItemAsync("Kitchen", spaceId: _home);
        await _client.PostJsonAsync($"/api/work-items/{tiles.Id}/links", new CreateWorkItemLinkRequest(kitchen.Id, WorkItemLinkRelation.Blocks));
        await _client.DeleteAsync($"/api/work-items/{door.Id}");

        var response = await MoveAsync(_client, shed.Id, new MoveWorkItemToSpaceRequest(_workshop));
        await _client.PostAsync($"/api/trash/{door.Id}/restore", null);

        var links = await (await _client.GetAsync($"/api/work-items/{tiles.Id}/links")).ReadAsync<List<WorkItemLinkDto>>();
        var workshopToDo = await ApiClient.StatusIdAsync(_workshop, "To Do");
        Assert.Multiple(async () =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            foreach (var id in new[] { shed.Id, roof.Id, tiles.Id, door.Id })
            {
                var moved = await GetAsync(id);
                Assert.That((moved.SpaceId, moved.StatusId), Is.EqualTo((_workshop, workshopToDo)), moved.Title);
            }

            Assert.That((await GetAsync(door.Id)).ParentId, Is.EqualTo(shed.Id));
            Assert.That(links.Single().LinkedWorkItemId, Is.EqualTo(kitchen.Id));
            Assert.That(links.Single().LinkedWorkItemSpaceId, Is.EqualTo(_home));
        });
    }

    [Test]
    public async Task Move_RecordsHistoryOnEveryMovedItem()
    {
        var shed = await _client.CreateWorkItemAsync("Shed", spaceId: _home);
        var roof = await _client.CreateWorkItemAsync("Roof", shed.Id);

        await MoveAsync(_client, shed.Id, new MoveWorkItemToSpaceRequest(_workshop));

        foreach (var id in new[] { shed.Id, roof.Id })
        {
            var history = await (await _client.GetAsync($"/api/work-items/{id}/history")).ReadAsync<List<WorkItemHistoryEntryDto>>();
            Assert.That(history.Count(e => e.Field == WorkItemField.SpaceId), Is.EqualTo(2), "created in Home, then moved");
        }
    }

    [Test]
    public async Task Move_WithAMapToAnotherSpacesStatus_Returns400_AndMovesNothing()
    {
        var shed = await _client.CreateWorkItemAsync("Shed", spaceId: _home);
        var roof = await _client.CreateWorkItemAsync("Roof", shed.Id);
        var homeToDo = await ApiClient.StatusIdAsync(_home, "To Do");
        var homeDone = await ApiClient.StatusIdAsync(_home, "Done");

        var response = await MoveAsync(_client, shed.Id, new MoveWorkItemToSpaceRequest(
            _workshop, StatusMap: new Dictionary<Guid, Guid> { [homeToDo] = homeDone }));

        Assert.Multiple(async () =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That((await GetAsync(roof.Id)).SpaceId, Is.EqualTo(_home));
        });
    }

    [Test]
    public async Task Reparent_IntoAnotherSpace_IsStillRefused()
    {
        var shed = await _client.CreateWorkItemAsync("Shed", spaceId: _home);
        var bench = await _client.CreateWorkItemAsync("Bench", spaceId: _workshop);

        var response = await _client.PutJsonAsync($"/api/work-items/{shed.Id}/parent", new ReparentWorkItemRequest(bench.Id, null));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task AReadOnlyKey_CanPlanAMove_ButNotMake_OverRestOrMcp()
    {
        var shed = await _client.CreateWorkItemAsync("Shed", spaceId: _home);
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);
        await using var mcp = await ConnectAsync(agent);

        var plan = await agent.GetAsync($"/api/work-items/{shed.Id}/space-move-plan?targetSpaceId={_workshop}");
        var restMove = await MoveAsync(agent, shed.Id, new MoveWorkItemToSpaceRequest(_workshop));
        var mcpPlan = await mcp.CallToolAsync("plan_work_item_space_move", new Dictionary<string, object?>
        {
            ["id"] = shed.Id,
            ["targetSpaceId"] = _workshop,
        });
        var mcpMove = await mcp.CallToolAsync("move_work_item_to_space", new Dictionary<string, object?>
        {
            ["id"] = shed.Id,
            ["targetSpaceId"] = _workshop,
        });

        Assert.Multiple(async () =>
        {
            Assert.That(plan.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That((await plan.ReadAsync<SpaceMovePlanDto>()).ItemCount, Is.EqualTo(1));
            Assert.That(restMove.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(mcpPlan.IsError, Is.Not.True);
            Assert.That(mcpMove.IsError, Is.True);
            Assert.That((await GetAsync(shed.Id)).SpaceId, Is.EqualTo(_home));
        });
    }

    [Test]
    public async Task AReadWriteKey_MovesOverMcp_AttributedToTheAgent()
    {
        var shed = await _client.CreateWorkItemAsync("Shed", spaceId: _home);
        var (agent, account) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);
        await using var mcp = await ConnectAsync(agent);

        var result = await mcp.CallToolAsync("move_work_item_to_space", new Dictionary<string, object?>
        {
            ["id"] = shed.Id,
            ["targetSpaceId"] = _workshop,
            ["actorLabel"] = "tidy-up",
        });

        var history = await (await _client.GetAsync($"/api/work-items/{shed.Id}/history")).ReadAsync<List<WorkItemHistoryEntryDto>>();
        var move = history.First(e => e.Field == WorkItemField.SpaceId);
        Assert.Multiple(async () =>
        {
            Assert.That(result.IsError, Is.Not.True, () => string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text)));
            Assert.That((await GetAsync(shed.Id)).SpaceId, Is.EqualTo(_workshop));
            Assert.That(move.AccountId, Is.EqualTo(account.Id));
            Assert.That(move.ActorLabel, Is.EqualTo("tidy-up"));
        });
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
