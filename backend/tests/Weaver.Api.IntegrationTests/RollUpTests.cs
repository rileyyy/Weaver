using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Weaver.Api.Contracts;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// Roll-ups against Postgres, whose recursive descendant walk the InMemory tests can't run: the
/// flag round-trips through the new column, and totals come back over REST and MCP without
/// trashed items.
/// </summary>
[TestFixture]
public class RollUpTests
{
    private HttpClient _admin = null!;
    private Guid _space;
    private Guid _room;

    [SetUp]
    public async Task SetUp()
    {
        _admin = await ApiClient.FirstAdminAsync();
        _space = await ApiClient.CreateSpaceAsync($"Roll-ups {Guid.NewGuid():N}"[..30]);
        var types = await (await _admin.GetAsync($"/api/work-item-types?spaceId={_space}"))
            .ReadAsync<IReadOnlyList<WorkItemTypeDto>>();
        _room = types.Single(t => t.Name == "Project").Id;
    }

    [Test]
    public async Task TheFlag_IsStoredAndListedForTheSpace()
    {
        var budget = await CreateBudgetAsync();

        var fields = await (await _admin.GetAsync($"/api/spaces/{_space}/custom-fields"))
            .ReadAsync<IReadOnlyList<CustomFieldDto>>();

        Assert.That(fields.Single(f => f.Id == budget.Id).RollsUp, Is.True);
    }

    [Test]
    public async Task Totals_SumLiveDescendantsAtAnyDepth_OverRestAndMcp()
    {
        var budget = await CreateBudgetAsync();
        var house = await CreateRoomAsync(null, budget, 1000);
        var kitchen = await CreateRoomAsync(house, budget, 200);
        await CreateRoomAsync(kitchen, budget, 50.25m);
        var trashed = await CreateRoomAsync(house, budget, 999);
        await _admin.DeleteAsync($"/api/work-items/{trashed.Id}");
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);
        await using var mcp = await ConnectAsync(agent);

        var rest = await (await _admin.GetAsync($"/api/work-items/{house.Id}/roll-ups"))
            .ReadAsync<IReadOnlyList<RollUpDto>>();
        var detail = await mcp.CallToolAsync("get_work_item", new Dictionary<string, object?> { ["id"] = house.Id });

        var mcpItem = JsonSerializer.Deserialize<WorkItemDto>(TextOf(detail), ApiClient.Json)!;
        Assert.Multiple(() =>
        {
            Assert.That(rest.Single().Total, Is.EqualTo(250.25m));
            Assert.That(mcpItem.RollUps!.Single().Total, Is.EqualTo(250.25m));
            Assert.That(mcpItem.RollUps!.Single().FieldId, Is.EqualTo(budget.Id));
        });
    }

    private async Task<CustomFieldDto> CreateBudgetAsync() =>
        await (await _admin.PostJsonAsync(
                "/api/custom-fields",
                new CreateCustomFieldRequest(_room, "Budget", CustomFieldKind.Currency, RollsUp: true)))
            .ReadAsync<CustomFieldDto>();

    private async Task<WorkItemDto> CreateRoomAsync(WorkItemDto? parent, CustomFieldDto budget, decimal amount)
    {
        var item = await _admin.CreateWorkItemAsync("Room", parent?.Id, parent is null ? _space : null);
        await _admin.PatchJsonAsync($"/api/work-items/{item.Id}/details", new { typeId = _room });
        await _admin.PatchJsonAsync(
            $"/api/work-items/{item.Id}/fields", new { values = new Dictionary<Guid, object?> { [budget.Id] = amount } });
        return item;
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
