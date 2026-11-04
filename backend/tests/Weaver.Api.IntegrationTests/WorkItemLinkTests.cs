using System.Net;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Weaver.Api.Contracts;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

/// <summary>Typed links, URL links and blocked-by resolving a When, over REST and MCP against Postgres.</summary>
[TestFixture]
public class WorkItemLinkTests
{
    private HttpClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        (_client, _) = await ApiClient.SignedInAsync();
    }

    [Test]
    public async Task ABlocksLink_ReadsAsBlockedByFromTheOtherSide_AcrossSpaces()
    {
        var garden = await ApiClient.CreateSpaceAsync("Garden");
        var drywall = await _client.CreateWorkItemAsync("Drywall");
        var paint = await _client.CreateWorkItemAsync("Paint", spaceId: garden);

        var created = await (await _client.PostJsonAsync(
            $"/api/work-items/{drywall.Id}/links",
            new CreateWorkItemLinkRequest(paint.Id, WorkItemLinkRelation.Blocks))).ReadAsync<WorkItemLinkDto>();
        var fromPaint = await (await _client.GetAsync($"/api/work-items/{paint.Id}/links")).ReadAsync<List<WorkItemLinkDto>>();

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.EqualTo(new WorkItemLinkDto(
                created.Id, WorkItemLinkRelation.Blocks, paint.Id, paint.Number, "Paint", garden)));
            Assert.That(fromPaint.Single(), Is.EqualTo(new WorkItemLinkDto(
                created.Id, WorkItemLinkRelation.BlockedBy, drywall.Id, drywall.Number, "Drywall", drywall.SpaceId)));
        });
    }

    [Test]
    public async Task TheTypeIsSentAsItsName()
    {
        var a = await _client.CreateWorkItemAsync("A");
        var b = await _client.CreateWorkItemAsync("B");

        var response = await _client.PostJsonAsync($"/api/work-items/{a.Id}/links", new { targetWorkItemId = b.Id, type = "DuplicatedBy" });
        var json = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(json, Does.Contain("\"type\":\"DuplicatedBy\""));
            Assert.That(json, Does.Contain($"\"linkedWorkItemNumber\":{b.Number}"));
        });
    }

    [Test]
    public async Task CreatingALink_WithoutAType_Is400()
    {
        var a = await _client.CreateWorkItemAsync("A");
        var b = await _client.CreateWorkItemAsync("B");

        var response = await _client.PostJsonAsync($"/api/work-items/{a.Id}/links", new { targetWorkItemId = b.Id });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CreatingALink_WithAnUnknownType_Is400()
    {
        var a = await _client.CreateWorkItemAsync("A");
        var b = await _client.CreateWorkItemAsync("B");

        var response = await _client.PostJsonAsync($"/api/work-items/{a.Id}/links", new { targetWorkItemId = b.Id, type = "Causes" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ABlocksCycle_Is409_AndADuplicateOfAnyType_Is409()
    {
        var a = await _client.CreateWorkItemAsync("A");
        var b = await _client.CreateWorkItemAsync("B");
        await _client.PostJsonAsync($"/api/work-items/{a.Id}/links", new CreateWorkItemLinkRequest(b.Id, WorkItemLinkRelation.Blocks));

        var cycle = await _client.PostJsonAsync($"/api/work-items/{b.Id}/links", new CreateWorkItemLinkRequest(a.Id, WorkItemLinkRelation.Blocks));
        var duplicate = await _client.PostJsonAsync($"/api/work-items/{b.Id}/links", new CreateWorkItemLinkRequest(a.Id, WorkItemLinkRelation.Related));

        Assert.Multiple(() =>
        {
            Assert.That(cycle.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(duplicate.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        });
    }

    [Test]
    public async Task AWhenWithALiveBlocker_IsResolved_UntilTheBlockerIsTrashed()
    {
        var space = await ApiClient.CreateSpaceAsync("Renovation");
        var drywall = await _client.CreateWorkItemAsync("Drywall", spaceId: space);
        var paint = await _client.CreateWorkItemAsync("Paint", spaceId: space);
        await _client.PatchJsonAsync($"/api/work-items/{paint.Id}/details", new { when = "after drywall" });

        await _client.PostJsonAsync($"/api/work-items/{paint.Id}/links", new CreateWorkItemLinkRequest(drywall.Id, WorkItemLinkRelation.BlockedBy));
        var blocked = await GetAsync(paint.Id);
        var searched = await (await _client.GetAsync($"/api/work-items/search?spaceIds={space}"))
            .ReadAsync<WorkItemPageDto>();
        await _client.DeleteAsync($"/api/work-items/{drywall.Id}");
        var blockerTrashed = await GetAsync(paint.Id);

        Assert.Multiple(() =>
        {
            Assert.That(blocked.WhenResolved, Is.True);
            Assert.That(searched.Items.Single(i => i.Id == paint.Id).WhenResolved, Is.True);
            Assert.That(blockerTrashed.WhenResolved, Is.False);
        });
    }

    [Test]
    public async Task UrlLinks_CanBeAddedListedAndRemoved()
    {
        var item = await _client.CreateWorkItemAsync("Kitchen");

        var created = await _client.PostJsonAsync($"/api/work-items/{item.Id}/url-links", new { url = " https://example.com/quote ", label = " " });
        var link = await created.ReadAsync<WorkItemUrlLinkDto>();
        var listed = await (await _client.GetAsync($"/api/work-items/{item.Id}/url-links")).ReadAsync<List<WorkItemUrlLinkDto>>();
        var deleted = await _client.DeleteAsync($"/api/work-item-url-links/{link.Id}");
        var deletedAgain = await _client.DeleteAsync($"/api/work-item-url-links/{link.Id}");

        Assert.Multiple(() =>
        {
            Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(link, Is.EqualTo(new WorkItemUrlLinkDto(link.Id, "https://example.com/quote", null)));
            Assert.That(listed, Is.EqualTo(new[] { link }));
            Assert.That(deleted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(deletedAgain.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task AUrlLink_ThatIsNotAWebAddress_Is400_AndOnAnUnknownItem_Is404()
    {
        var item = await _client.CreateWorkItemAsync("Kitchen");

        var invalid = await _client.PostJsonAsync($"/api/work-items/{item.Id}/url-links", new CreateWorkItemUrlLinkRequest("javascript:alert(1)"));
        var unknown = await _client.PostJsonAsync($"/api/work-items/{Guid.NewGuid()}/url-links", new CreateWorkItemUrlLinkRequest("https://example.com"));

        Assert.Multiple(() =>
        {
            Assert.That(invalid.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(unknown.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task Mcp_CreatesATypedLinkAndAUrlLink()
    {
        var a = await _client.CreateWorkItemAsync("A");
        var b = await _client.CreateWorkItemAsync("B");
        await using var mcp = await ConnectAsync();

        var link = await mcp.CallToolAsync("create_work_item_link", new Dictionary<string, object?>
        {
            ["workItemId"] = a.Id,
            ["targetWorkItemId"] = b.Id,
            ["type"] = "BlockedBy",
        });
        var urlLink = await mcp.CallToolAsync("create_work_item_url_link", new Dictionary<string, object?>
        {
            ["workItemId"] = a.Id,
            ["url"] = "https://github.com/pr/1",
        });
        var fromB = await (await _client.GetAsync($"/api/work-items/{b.Id}/links")).ReadAsync<List<WorkItemLinkDto>>();

        Assert.Multiple(() =>
        {
            Assert.That(link.IsError, Is.Not.True, Text(link));
            Assert.That(urlLink.IsError, Is.Not.True, Text(urlLink));
            Assert.That(JsonSerializer.Deserialize<WorkItemLinkDto>(Text(link), ApiClient.Json)!.Type, Is.EqualTo(WorkItemLinkRelation.BlockedBy));
            Assert.That(fromB.Single().Type, Is.EqualTo(WorkItemLinkRelation.Blocks));
        });
    }

    private async Task<WorkItemDto> GetAsync(Guid id) =>
        await (await _client.GetAsync($"/api/work-items/{id}")).ReadAsync<WorkItemDto>();

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

    private static string Text(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));
}
