using System.Net;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Weaver.Api.Contracts;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// Per-Space statuses and priorities over REST and MCP, against Postgres: the per-Space unique
/// order index, and the rule that a work item only takes its own Space's values.
/// </summary>
[TestFixture]
public class PerSpaceLookupTests
{
    [Test]
    public async Task ANewSpace_ListsItsOwnDefaultStatusesAndPriorities()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var space = await ApiClient.CreateSpaceAsync("Defaults");

        var statuses = await (await client.GetAsync($"/api/statuses?spaceId={space}"))
            .ReadAsync<IReadOnlyList<StatusDto>>();
        var priorities = await (await client.GetAsync($"/api/work-item-priorities?spaceId={space}"))
            .ReadAsync<IReadOnlyList<WorkItemPriorityDto>>();

        Assert.Multiple(() =>
        {
            Assert.That(statuses.Select(s => s.Name), Is.EqualTo(new[] { "To Do", "Doing", "Done" }));
            Assert.That(statuses.Select(s => s.SpaceId), Is.All.EqualTo(space));
            Assert.That(priorities.Select(p => p.Name), Is.EqualTo(new[] { "Low", "Medium", "High", "Urgent" }));
        });
    }

    [Test]
    public async Task ListingStatuses_WithoutASpace_Returns400()
    {
        var (client, _) = await ApiClient.SignedInAsync();

        var response = await client.GetAsync("/api/statuses");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task TwoSpaces_CanReorderTheirStatusesIndependently()
    {
        var admin = await ApiClient.FirstAdminAsync();
        var home = await ApiClient.CreateSpaceAsync("Home");
        var software = await ApiClient.CreateSpaceAsync("Software");
        var softwareStatuses = await (await admin.GetAsync($"/api/statuses?spaceId={software}"))
            .ReadAsync<IReadOnlyList<StatusDto>>();

        var response = await admin.PutJsonAsync(
            $"/api/statuses/order?spaceId={software}",
            new ReorderLookupRequest(softwareStatuses.Select(s => s.Id).Reverse().ToList()));

        var homeStatuses = await (await admin.GetAsync($"/api/statuses?spaceId={home}"))
            .ReadAsync<IReadOnlyList<StatusDto>>();
        Assert.Multiple(async () =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That((await response.ReadAsync<IReadOnlyList<StatusDto>>()).Select(s => s.Name),
                Is.EqualTo(new[] { "Done", "Doing", "To Do" }));
            Assert.That(homeStatuses.Select(s => s.Name), Is.EqualTo(new[] { "To Do", "Doing", "Done" }));
        });
    }

    [Test]
    public async Task CreatingAStatus_AddsItToTheEndOfThatSpaceOnly()
    {
        var admin = await ApiClient.FirstAdminAsync();
        var home = await ApiClient.CreateSpaceAsync("Home");
        var software = await ApiClient.CreateSpaceAsync("Software");

        var response = await admin.PostJsonAsync(
            "/api/statuses",
            new CreateStatusRequest(home, "Quoted", StatusCategory.ToDo, "#1E88E5"));

        var created = await response.ReadAsync<StatusDto>();
        var softwareStatuses = await (await admin.GetAsync($"/api/statuses?spaceId={software}"))
            .ReadAsync<IReadOnlyList<StatusDto>>();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(created.SpaceId, Is.EqualTo(home));
            Assert.That(created.Order, Is.EqualTo(3));
            Assert.That(softwareStatuses.Select(s => s.Name), Does.Not.Contain("Quoted"));
        });
    }

    [Test]
    public async Task MovingAnItemToAnotherSpacesStatus_Returns400AndChangesNothing()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var home = await ApiClient.CreateSpaceAsync("Home");
        var software = await ApiClient.CreateSpaceAsync("Software");
        var item = await client.CreateWorkItemAsync("Fence", spaceId: home);

        var response = await client.PutJsonAsync(
            $"/api/work-items/{item.Id}/status",
            new ChangeWorkItemStatusRequest(await ApiClient.StatusIdAsync(software, "Done"), null));

        var after = await (await client.GetAsync($"/api/work-items/{item.Id}")).ReadAsync<WorkItemDto>();
        Assert.Multiple(async () =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("another Space"));
            Assert.That(after.StatusId, Is.EqualTo(item.StatusId));
            Assert.That(after.Version, Is.EqualTo(item.Version));
        });
    }

    [Test]
    public async Task ListStatuses_OverMcp_ReturnsTheGivenSpacesStatuses()
    {
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);
        var space = await ApiClient.CreateSpaceAsync("Software");
        await using var mcp = await ConnectAsync(agent);

        var result = await mcp.CallToolAsync("list_statuses", new Dictionary<string, object?> { ["spaceId"] = space });

        var statuses = JsonSerializer.Deserialize<List<StatusDto>>(TextOf(result), ApiClient.Json)!;
        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.Not.True);
            Assert.That(statuses.Select(s => s.SpaceId), Is.All.EqualTo(space));
            Assert.That(statuses.Select(s => s.Name), Is.EqualTo(new[] { "To Do", "Doing", "Done" }));
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
