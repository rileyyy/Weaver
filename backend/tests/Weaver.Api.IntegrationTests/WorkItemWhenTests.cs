using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Weaver.Api.Contracts;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

/// <summary>The When phrase over REST and MCP against Postgres, and applying a quick pick.</summary>
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
