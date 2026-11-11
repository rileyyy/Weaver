using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Weaver.Api.Contracts;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// The When phrase over REST and MCP against Postgres, applying a quick pick, and agents listing
/// and resolving unresolved Whens.
/// </summary>
[TestFixture]
public class WorkItemWhenTests
{
    private HttpClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        (_client, _) = await ApiClient.SignedInAsync();
    }

    [Test]
    public async Task PatchDetails_WithAWhenButNoDates_StoresItUnresolved()
    {
        var item = await _client.CreateWorkItemAsync("Paint the hall");

        var response = await PatchAsync("details", item, new { when = "after drywall" });
        var reloaded = await GetAsync(item);

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.When, Is.EqualTo("after drywall"));
            Assert.That(reloaded.WhenResolved, Is.False);
            Assert.That(response.ChangedFields, Is.EqualTo(new[] { "when" }));
        });
    }

    [Test]
    public async Task ApplyingAQuickPick_ThroughTheSchedule_ResolvesTheWhenAndKeepsThePhrase()
    {
        var item = await _client.CreateWorkItemAsync("Clean the gutters");
        await PatchAsync("details", item, new { when = "next week" });
        var picks = await (await _client.GetAsync("/api/quick-picks")).ReadAsync<List<QuickPickDto>>();
        var nextWeek = picks.Single(p => p.Pick == QuickPick.NextWeek);

        var response = await PatchAsync("schedule", item, new { startDate = nextWeek.StartDate, endDate = nextWeek.EndDate });

        Assert.Multiple(() =>
        {
            Assert.That(picks.Select(p => p.Pick), Is.EqualTo(Enum.GetValues<QuickPick>()));
            Assert.That(nextWeek.StartDate.DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
            Assert.That(response.Item.When, Is.EqualTo("next week"));
            Assert.That(response.Item.WhenResolved, Is.True);
        });
    }

    [Test]
    public async Task Mcp_UpdateDetails_SetsTheWhenAndLeavesTheRestAlone()
    {
        var item = await _client.CreateWorkItemAsync("Bake for the market");
        await using var mcp = await ConnectAsync();

        var result = await mcp.CallToolAsync("update_work_item_details", new Dictionary<string, object?>
        {
            ["id"] = item.Id,
            ["when"] = "before the Christmas market",
        });
        var reloaded = await GetAsync(item);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.Not.True, Text(result));
            Assert.That(reloaded.When, Is.EqualTo("before the Christmas market"));
            Assert.That(reloaded.Title, Is.EqualTo("Bake for the market"));
            Assert.That(Structured(result).Item.WhenResolved, Is.False);
        });
    }

    [Test]
    public async Task Mcp_ListUnresolvedWhenItems_ListsThem_UntilTheScheduleOrABlockedByLinkResolvesThem()
    {
        var space = await ApiClient.CreateSpaceAsync("Unresolved When");
        var kitchen = await _client.CreateWorkItemAsync("Kitchen", spaceId: space);
        var drywall = await _client.CreateWorkItemAsync("Drywall", parentId: kitchen.Id);
        var paint = await _client.CreateWorkItemAsync("Paint the walls", parentId: kitchen.Id);
        var gutters = await _client.CreateWorkItemAsync("Clean the gutters", spaceId: space);
        await PatchAsync("details", paint, new { when = "after drywall" });
        await PatchAsync("details", gutters, new { when = "next spring" });
        await using var mcp = await ConnectAsync();

        var before = await ListUnresolvedAsync(mcp, space);
        await CallAsync(mcp, "reschedule_work_item", new() { ["id"] = gutters.Id, ["startDate"] = "2027-03-20" });
        await CallAsync(mcp, "create_work_item_link", new()
        {
            ["workItemId"] = paint.Id,
            ["targetWorkItemId"] = drywall.Id,
            ["type"] = "BlockedBy",
        });
        var after = await ListUnresolvedAsync(mcp, space);
        var reloaded = await GetAsync(paint);

        Assert.Multiple(() =>
        {
            Assert.That(before.Items.Select(i => (i.Id, i.When)), Is.EqualTo(new[] { (paint.Id, "after drywall"), (gutters.Id, "next spring") }));
            Assert.That(before.Items[0].ParentId, Is.EqualTo(kitchen.Id));
            Assert.That(before.Items[0].SpaceId, Is.EqualTo(space));
            Assert.That(before.Items[0].SpaceName, Does.StartWith("Unresolved When"));
            Assert.That(after.Items, Is.Empty);
            Assert.That(reloaded.When, Is.EqualTo("after drywall"));
            Assert.That(reloaded.WhenResolved, Is.True);
        });
    }

    [Test]
    public async Task Search_UnresolvedWhenOnly_ListsUnresolvedItems_WithFullDetail()
    {
        var space = await ApiClient.CreateSpaceAsync("Unresolved When REST");
        var kitchen = await _client.CreateWorkItemAsync("Kitchen", spaceId: space);
        var paint = await _client.CreateWorkItemAsync("Paint the walls", parentId: kitchen.Id);
        var scheduled = await _client.CreateWorkItemAsync("Clean the gutters", spaceId: space);
        await _client.CreateWorkItemAsync("No phrase", spaceId: space);
        await PatchAsync("details", paint, new { when = "after drywall" });
        await PatchAsync("details", scheduled, new { when = "next spring" });
        await PatchAsync("schedule", scheduled, new { startDate = "2027-03-20" });

        var page = await (await _client.GetAsync($"/api/work-items/search?spaceIds={space}&unresolvedWhenOnly=true"))
            .ReadAsync<WorkItemPageDto>();

        Assert.Multiple(() =>
        {
            Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { paint.Id }));
            Assert.That(page.Items[0].When, Is.EqualTo("after drywall"));
            Assert.That(page.Items[0].ParentTitle, Is.EqualTo("Kitchen"));
        });
    }

    private static async Task<WorkItemSummaryPageDto> ListUnresolvedAsync(McpClient mcp, Guid space)
    {
        var result = await mcp.CallToolAsync("list_unresolved_when_items", new Dictionary<string, object?>
        {
            ["spaceIds"] = new[] { space },
        });
        Assert.That(result.IsError, Is.Not.True, Text(result));
        return JsonSerializer.Deserialize<WorkItemSummaryPageDto>(Text(result), ApiClient.Json)!;
    }

    private static async Task CallAsync(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, arguments);
        Assert.That(result.IsError, Is.Not.True, Text(result));
    }

    private async Task<WorkItemWriteResponse> PatchAsync<T>(string operation, WorkItemDto item, T body) =>
        await (await _client.PatchJsonAsync($"/api/work-items/{item.Id}/{operation}", body)).ReadAsync<WorkItemWriteResponse>();

    private async Task<WorkItemDto> GetAsync(WorkItemDto item) =>
        await (await _client.GetAsync($"/api/work-items/{item.Id}")).ReadAsync<WorkItemDto>();

    private Task<McpClient> ConnectAsync() =>
        McpClient.CreateAsync(new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(_client.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            _client,
            loggerFactory: null,
            ownsHttpClient: false));

    private static WorkItemWriteResponse Structured(CallToolResult result) =>
        JsonSerializer.Deserialize<WorkItemWriteResponse>(Text(result), ApiClient.Json)!;

    private static string Text(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));
}
