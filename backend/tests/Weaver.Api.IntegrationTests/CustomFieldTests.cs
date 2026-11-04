using System.Net;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Weaver.Api.Contracts;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// Custom fields over REST and MCP against Postgres: definitions per Type, values round-tripping
/// through the jsonb column, and the rules for history, Type changes and removed fields.
/// </summary>
[TestFixture]
public class CustomFieldTests
{
    private HttpClient _admin = null!;
    private Guid _space;
    private Guid _room;
    private Guid _chore;

    [SetUp]
    public async Task SetUp()
    {
        _admin = await ApiClient.FirstAdminAsync();
        _space = await ApiClient.CreateSpaceAsync($"Fields {Guid.NewGuid():N}"[..30]);
        var types = await (await _admin.GetAsync($"/api/work-item-types?spaceId={_space}"))
            .ReadAsync<IReadOnlyList<WorkItemTypeDto>>();
        _room = types.Single(t => t.Name == "Project").Id;
        _chore = types.Single(t => t.Name == "Task").Id;
    }

    [Test]
    public async Task EveryKindOfValue_RoundTripsThroughTheDatabase()
    {
        var floor = await CreateFieldAsync(_room, "Floor", CustomFieldKind.MultiSelect, "Oak", "Tiles");
        var budget = await CreateFieldAsync(_room, "Budget", CustomFieldKind.Currency);
        var due = await CreateFieldAsync(_room, "Due", CustomFieldKind.Date);
        var done = await CreateFieldAsync(_room, "Inspected", CustomFieldKind.Checkbox);
        var item = await CreateRoomAsync();

        var response = await PatchFieldsAsync(item, new Dictionary<Guid, object?>
        {
            [floor.Id] = new[] { floor.Options[1].Id, floor.Options[0].Id },
            [budget.Id] = 1200.50m,
            [due.Id] = "2026-12-24",
            [done.Id] = true,
        });

        var reloaded = await GetAsync(item);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(
                reloaded.Fields[floor.Id].EnumerateArray().Select(e => e.GetGuid()),
                Is.EqualTo(new[] { floor.Options[0].Id, floor.Options[1].Id }));
            Assert.That(reloaded.Fields[budget.Id].GetDecimal(), Is.EqualTo(1200.5m));
            Assert.That(reloaded.Fields[due.Id].GetString(), Is.EqualTo("2026-12-24"));
            Assert.That(reloaded.Fields[done.Id].GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task AValueTheFieldRejects_Returns400AndChangesNothing()
    {
        var budget = await CreateFieldAsync(_room, "Budget", CustomFieldKind.Currency);
        var item = await CreateRoomAsync();

        var response = await PatchFieldsAsync(item, new Dictionary<Guid, object?> { [budget.Id] = "a lot" });

        var reloaded = await GetAsync(item);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(reloaded.Fields, Is.Empty);
            Assert.That(reloaded.Version, Is.EqualTo(item.Version));
        });
    }

    [Test]
    public async Task AStaleExpectedVersion_Returns409()
    {
        var budget = await CreateFieldAsync(_room, "Budget", CustomFieldKind.Currency);
        var item = await CreateRoomAsync();
        await PatchFieldsAsync(item, new Dictionary<Guid, object?> { [budget.Id] = 1 });

        var response = await PatchFieldsAsync(item, new Dictionary<Guid, object?> { [budget.Id] = 2 }, item.Version);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task AChange_IsInHistory_NamedForTheField()
    {
        var budget = await CreateFieldAsync(_room, "Budget", CustomFieldKind.Currency);
        var item = await CreateRoomAsync();
        await PatchFieldsAsync(item, new Dictionary<Guid, object?> { [budget.Id] = 1200 });

        var history = await (await _admin.GetAsync($"/api/work-items/{item.Id}/history"))
            .ReadAsync<IReadOnlyList<WorkItemHistoryEntryDto>>();

        var entry = history.Single(e => e.Field == $"fields.{budget.Id}");
        Assert.Multiple(() =>
        {
            Assert.That(entry.FieldName, Is.EqualTo("Budget"));
            Assert.That(entry.NewValue?.GetDecimal(), Is.EqualTo(1200m));
        });
    }

    [Test]
    public async Task ChangingTheType_ClearsTheValues()
    {
        var budget = await CreateFieldAsync(_room, "Budget", CustomFieldKind.Currency);
        var item = await CreateRoomAsync();
        await PatchFieldsAsync(item, new Dictionary<Guid, object?> { [budget.Id] = 1200 });

        var response = await _admin.PatchJsonAsync($"/api/work-items/{item.Id}/details", new { typeId = _chore });

        var write = await response.ReadAsync<WorkItemWriteResponse>();
        Assert.Multiple(() =>
        {
            Assert.That(write.Item.Fields, Is.Empty);
            Assert.That(write.ChangedFields, Is.EquivalentTo(new[] { "typeId", $"fields.{budget.Id}" }));
        });
    }

    [Test]
    public async Task RemovingAField_RemovesItsValues()
    {
        var budget = await CreateFieldAsync(_room, "Budget", CustomFieldKind.Currency);
        var item = await CreateRoomAsync();
        await PatchFieldsAsync(item, new Dictionary<Guid, object?> { [budget.Id] = 1200 });

        var response = await _admin.DeleteAsync($"/api/custom-fields/{budget.Id}");

        Assert.Multiple(async () =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That((await GetAsync(item)).Fields, Is.Empty);
        });
    }

    [Test]
    public async Task ReorderingFields_SwapsThemDespiteTheUniqueOrder()
    {
        var budget = await CreateFieldAsync(_room, "Budget", CustomFieldKind.Currency);
        var due = await CreateFieldAsync(_room, "Due", CustomFieldKind.Date);

        var response = await _admin.PutJsonAsync(
            $"/api/custom-fields/order?typeId={_room}", new ReorderLookupRequest([due.Id, budget.Id]));

        var fields = await response.ReadAsync<IReadOnlyList<CustomFieldDto>>();
        Assert.That(fields.Select(f => f.Name), Is.EqualTo(new[] { "Due", "Budget" }));
    }

    [Test]
    public async Task AMember_CannotDefineFields()
    {
        var (member, _) = await ApiClient.SignedInAsync();

        var response = await member.PostJsonAsync(
            "/api/custom-fields", new CreateCustomFieldRequest(_room, "Budget", CustomFieldKind.Currency));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task OverMcp_AnAgentReadsTheDefinitionsAndSetsAValue()
    {
        var floor = await CreateFieldAsync(_room, "Floor", CustomFieldKind.SingleSelect, "Oak", "Tiles");
        var item = await CreateRoomAsync();
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);
        await using var mcp = await ConnectAsync(agent);

        var list = await mcp.CallToolAsync("list_work_item_types", new Dictionary<string, object?> { ["spaceId"] = _space });
        var set = await mcp.CallToolAsync("set_work_item_fields", new Dictionary<string, object?>
        {
            ["id"] = item.Id,
            ["values"] = new Dictionary<string, object?> { [floor.Id.ToString()] = floor.Options[1].Id },
        });

        var types = JsonSerializer.Deserialize<List<WorkItemTypeWithFieldsDto>>(TextOf(list), ApiClient.Json)!;
        Assert.Multiple(async () =>
        {
            Assert.That(types.Single(t => t.Id == _room).Fields.Single().Name, Is.EqualTo("Floor"));
            Assert.That(set.IsError, Is.Not.True, TextOf(set));
            Assert.That((await GetAsync(item)).Fields[floor.Id].GetGuid(), Is.EqualTo(floor.Options[1].Id));
        });
    }

    private async Task<CustomFieldDto> CreateFieldAsync(Guid typeId, string name, CustomFieldKind kind, params string[] options) =>
        await (await _admin.PostJsonAsync("/api/custom-fields", new CreateCustomFieldRequest(typeId, name, kind, options)))
            .ReadAsync<CustomFieldDto>();

    private async Task<WorkItemDto> CreateRoomAsync()
    {
        var item = await _admin.CreateWorkItemAsync("Kitchen", spaceId: _space);
        var response = await _admin.PatchJsonAsync($"/api/work-items/{item.Id}/details", new { typeId = _room });
        return (await response.ReadAsync<WorkItemWriteResponse>()).Item;
    }

    private Task<HttpResponseMessage> PatchFieldsAsync(WorkItemDto item, Dictionary<Guid, object?> values, uint? expectedVersion = null) =>
        _admin.PatchJsonAsync($"/api/work-items/{item.Id}/fields", new { values, expectedVersion });

    private async Task<WorkItemDto> GetAsync(WorkItemDto item) =>
        await (await _admin.GetAsync($"/api/work-items/{item.Id}")).ReadAsync<WorkItemDto>();

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
