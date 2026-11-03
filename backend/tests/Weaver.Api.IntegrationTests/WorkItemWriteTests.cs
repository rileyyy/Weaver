using System.Net;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Weaver.Api.Contracts;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// Patch semantics (a field left out is unchanged, <c>null</c> clears it) and
/// <c>expectedVersion</c> on every work-item write, over REST and MCP, against Postgres's real
/// <c>xmin</c> versions.
/// </summary>
[TestFixture]
public class WorkItemWriteTests
{
    private HttpClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        (_client, _) = await ApiClient.SignedInAsync();
    }

    [Test]
    public async Task PatchDetails_WithOnlyATitle_KeepsTheOtherDetails()
    {
        var item = await CreateDetailedItemAsync();

        var response = await PatchAsync($"details", item, new { title = "Renamed" });
        var reloaded = await GetAsync(item);
        var high = await ApiClient.PriorityIdAsync(item.SpaceId, "High");

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Title, Is.EqualTo("Renamed"));
            Assert.That(reloaded.Description, Is.EqualTo("Keep me"));
            Assert.That(reloaded.PriorityId, Is.EqualTo(high));
            Assert.That(response.ChangedFields, Is.EqualTo(new[] { "title" }));
            Assert.That(response.Item.Version, Is.Not.EqualTo(item.Version));
            Assert.That(response.Item.Version, Is.EqualTo(reloaded.Version));
        });
    }

    [Test]
    public async Task PatchDetails_WithNulls_ClearsThoseFields()
    {
        var item = await CreateDetailedItemAsync();

        var response = await PatchAsync("details", item, new { description = (string?)null, priorityId = (Guid?)null });
        var reloaded = await GetAsync(item);

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Title, Is.EqualTo(item.Title));
            Assert.That(reloaded.Description, Is.Null);
            Assert.That(reloaded.PriorityId, Is.Null);
            Assert.That(response.ChangedFields, Is.EquivalentTo(new[] { "description", "priorityId" }));
        });
    }

    [Test]
    public async Task PatchDetails_ClearingTheTitle_Returns400()
    {
        var item = await _client.CreateWorkItemAsync("Titled");

        var response = await _client.PatchJsonAsync(
            $"/api/work-items/{item.Id}/details", new { title = (string?)null });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PatchDetails_ToTheCurrentValues_ChangesNothingAndKeepsTheVersion()
    {
        var item = await _client.CreateWorkItemAsync("Same");

        var response = await PatchAsync("details", item, new { title = "Same", description = (string?)null });

        Assert.Multiple(() =>
        {
            Assert.That(response.ChangedFields, Is.Empty);
            Assert.That(response.Item.Version, Is.EqualTo(item.Version));
        });
    }

    [Test]
    public async Task PatchSchedule_LeavesOutOneDate_AndClearsTheOtherWithNull()
    {
        var item = await _client.CreateWorkItemAsync("Scheduled");
        await PatchAsync("schedule", item, new { startDate = "2026-09-01", endDate = "2026-09-05" });

        var endOnly = await PatchAsync("schedule", item, new { endDate = "2026-09-10" });
        var startCleared = await PatchAsync("schedule", item, new { startDate = (string?)null });
        var reloaded = await GetAsync(item);

        Assert.Multiple(() =>
        {
            Assert.That(endOnly.Item.StartDate, Is.EqualTo(new DateOnly(2026, 9, 1)));
            Assert.That(endOnly.ChangedFields, Is.EqualTo(new[] { "endDate" }));
            Assert.That(startCleared.ChangedFields, Is.EqualTo(new[] { "startDate" }));
            Assert.That(reloaded.StartDate, Is.Null);
            Assert.That(reloaded.EndDate, Is.EqualTo(new DateOnly(2026, 9, 10)));
        });
    }

    [Test]
    public async Task PatchTags_LeftOutKeepsThem_AndNullClearsThem()
    {
        var item = await _client.CreateWorkItemAsync("Tagged");
        await PatchAsync("tags", item, new { tags = new[] { "urgent" } });

        var leftOut = await PatchAsync("tags", item, new { });
        var cleared = await PatchAsync("tags", item, new { tags = (string[]?)null });

        Assert.Multiple(() =>
        {
            Assert.That(leftOut.Item.Tags, Is.EqualTo(new[] { "urgent" }));
            Assert.That(leftOut.ChangedFields, Is.Empty);
            Assert.That(cleared.Item.Tags, Is.Empty);
            Assert.That(cleared.ChangedFields, Is.EqualTo(new[] { "tags" }));
        });
    }

    // One case per work-item write: each must reject a version the item has moved past.
    private static IEnumerable<TestCaseData> StaleWrites()
    {
        yield return Stale("details", (c, item, v) => c.PatchJsonAsync(Path(item, "details"), new { title = "Stale", expectedVersion = v }));
        yield return Stale("schedule", (c, item, v) => c.PatchJsonAsync(Path(item, "schedule"), new { endDate = "2026-10-01", expectedVersion = v }));
        yield return Stale("tags", (c, item, v) => c.PatchJsonAsync(Path(item, "tags"), new { tags = new[] { "stale" }, expectedVersion = v }));
        yield return Stale("status", async (c, item, v) => await c.PutJsonAsync(
            Path(item, "status"),
            new ChangeWorkItemStatusRequest(await ApiClient.StatusIdAsync(item.SpaceId, "Doing"), null, v)));
        yield return Stale("parent", (c, item, v) => c.PutJsonAsync(Path(item, "parent"), new ReparentWorkItemRequest(null, null, v)));
        yield return Stale("assignee", (c, item, v) => c.PutJsonAsync(Path(item, "assignee"), new AssignWorkItemRequest(null, v)));
        yield return Stale("delete", (c, item, v) => c.DeleteAsync($"/api/work-items/{item.Id}?expectedVersion={v}"));
    }

    [TestCaseSource(nameof(StaleWrites))]
    public async Task AWrite_WithAStaleVersion_Returns409AndChangesNothing(
        Func<HttpClient, WorkItemDto, uint, Task<HttpResponseMessage>> write)
    {
        var item = await _client.CreateWorkItemAsync("Original");
        var edited = await PatchAsync("details", item, new { title = "Edited elsewhere" });

        var response = await write(_client, item, item.Version);
        var reloaded = await GetAsync(item);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(reloaded.Version, Is.EqualTo(edited.Item.Version), "the item must not have been written");
        });
    }

    [TestCaseSource(nameof(StaleWrites))]
    public async Task AWrite_WithTheCurrentVersion_Succeeds(
        Func<HttpClient, WorkItemDto, uint, Task<HttpResponseMessage>> write)
    {
        var item = await _client.CreateWorkItemAsync("Current", (await _client.CreateWorkItemAsync("Parent")).Id);

        var response = await write(_client, item, item.Version);

        Assert.That(response.IsSuccessStatusCode, Is.True, await response.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task Mcp_UpdateDetails_WithOnlyATitle_KeepsTheOtherDetailsAndReportsTheChange()
    {
        var item = await CreateDetailedItemAsync();
        await using var mcp = await ConnectAsync();

        var result = await mcp.CallToolAsync("update_work_item_details", new Dictionary<string, object?>
        {
            ["id"] = item.Id,
            ["title"] = "Renamed by an agent",
            ["expectedVersion"] = item.Version,
        });
        var reloaded = await GetAsync(item);
        var high = await ApiClient.PriorityIdAsync(item.SpaceId, "High");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.Not.True, Text(result));
            Assert.That(reloaded.Title, Is.EqualTo("Renamed by an agent"));
            Assert.That(reloaded.Description, Is.EqualTo("Keep me"));
            Assert.That(reloaded.PriorityId, Is.EqualTo(high));
            Assert.That(Structured(result).ChangedFields, Is.EqualTo(new[] { "title" }));
            Assert.That(Structured(result).Item.Version, Is.EqualTo(reloaded.Version));
        });
    }

    [Test]
    public async Task Mcp_UpdateDetails_WithANull_ClearsThatField()
    {
        var item = await CreateDetailedItemAsync();
        await using var mcp = await ConnectAsync();

        var result = await mcp.CallToolAsync("update_work_item_details", new Dictionary<string, object?>
        {
            ["id"] = item.Id,
            ["description"] = null,
        });
        var reloaded = await GetAsync(item);
        var high = await ApiClient.PriorityIdAsync(item.SpaceId, "High");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.Not.True, Text(result));
            Assert.That(reloaded.Title, Is.EqualTo(item.Title));
            Assert.That(reloaded.Description, Is.Null);
            Assert.That(reloaded.PriorityId, Is.EqualTo(high));
        });
    }

    [Test]
    public async Task Mcp_ChangeStatus_WithAStaleVersion_IsRejected()
    {
        var item = await _client.CreateWorkItemAsync("Original");
        await PatchAsync("details", item, new { title = "Edited elsewhere" });
        await using var mcp = await ConnectAsync();

        var result = await mcp.CallToolAsync("change_work_item_status", new Dictionary<string, object?>
        {
            ["id"] = item.Id,
            ["statusId"] = await ApiClient.StatusIdAsync(item.SpaceId, "Doing"),
            ["expectedVersion"] = item.Version,
        });
        var reloaded = await GetAsync(item);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.True);
            Assert.That(Text(result), Does.Contain("changed by someone else"));
            Assert.That(reloaded.StatusId, Is.EqualTo(item.StatusId));
        });
    }

    private static TestCaseData Stale(string name, Func<HttpClient, WorkItemDto, uint, Task<HttpResponseMessage>> write) =>
        new TestCaseData(write).SetArgDisplayNames(name);

    private static string Path(WorkItemDto item, string operation) => $"/api/work-items/{item.Id}/{operation}";

    private async Task<WorkItemDto> CreateDetailedItemAsync()
    {
        var item = await _client.CreateWorkItemAsync("Detailed");
        var high = await ApiClient.PriorityIdAsync(item.SpaceId, "High");
        return (await PatchAsync("details", item, new { description = "Keep me", priorityId = high })).Item;
    }

    private async Task<WorkItemWriteResponse> PatchAsync<T>(string operation, WorkItemDto item, T body) =>
        await (await _client.PatchJsonAsync(Path(item, operation), body)).ReadAsync<WorkItemWriteResponse>();

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
