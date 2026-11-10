using System.Net;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Weaver.Api.Contracts;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// Views against Postgres: the array columns, the unique name, the default View's cascades, and
/// listing and running Views over MCP. View names are shared by the whole run, so each is unique.
/// </summary>
[TestFixture]
public class ViewTests
{
    [Test]
    public async Task ASavedView_RoundTripsEveryInput_AndIsSharedWithOtherUsers()
    {
        var (riley, _) = await ApiClient.SignedInAsync();
        var (sam, _) = await ApiClient.SignedInAsync();
        var home = await ApiClient.CreateSpaceAsync("Home");
        var village = await ApiClient.CreateSpaceAsync("Village");
        var name = UniqueName("Outside work");

        var created = await (await riley.PostJsonAsync("/api/views", new SaveViewRequest(
            name,
            [home, village],
            "fence",
            ["Outside", "urgent"],
            TagMatch.All,
            ApplyDoneHiding: false,
            ScheduledOrInProgressOnly: true,
            ViewLayout.Roadmap))).ReadAsync<ViewDto>();
        var seenBySam = await (await sam.GetAsync($"/api/views/{created.Id}")).ReadAsync<ViewDto>();
        var listedForSam = await (await sam.GetAsync("/api/views")).ReadAsync<IReadOnlyList<ViewDto>>();

        Assert.Multiple(() =>
        {
            Assert.That(seenBySam.Name, Is.EqualTo(name));
            Assert.That(seenBySam.SpaceIds, Is.EqualTo(new[] { home, village }));
            Assert.That(seenBySam.Text, Is.EqualTo("fence"));
            Assert.That(seenBySam.Tags, Is.EqualTo(new[] { "Outside", "urgent" }));
            Assert.That(seenBySam.TagMatch, Is.EqualTo(TagMatch.All));
            Assert.That(seenBySam.ApplyDoneHiding, Is.False);
            Assert.That(seenBySam.ScheduledOrInProgressOnly, Is.True);
            Assert.That(seenBySam.Layout, Is.EqualTo(ViewLayout.Roadmap));
            Assert.That(listedForSam[0].Name, Is.EqualTo("Overview"));
            Assert.That(listedForSam[0].BuiltIn, Is.True);
            Assert.That(listedForSam.Select(v => v.Id), Does.Contain(created.Id));
        });
    }

    [Test]
    public async Task RenamingAndDeleting_ChangeTheStoredView()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var home = await ApiClient.CreateSpaceAsync("Home");
        var view = await CreateAsync(client, UniqueName("Home"), home);
        var renamed = UniqueName("Renamed");

        var put = await client.PutJsonAsync($"/api/views/{view.Id}", new SaveViewRequest(renamed, [home]));
        var afterRename = await (await client.GetAsync($"/api/views/{view.Id}")).ReadAsync<ViewDto>();
        var delete = await client.DeleteAsync($"/api/views/{view.Id}");
        var afterDelete = await client.GetAsync($"/api/views/{view.Id}");

        Assert.Multiple(() =>
        {
            Assert.That(put.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(afterRename.Name, Is.EqualTo(renamed));
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(afterDelete.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task ANameAlreadyTaken_IgnoringCase_Returns409()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var name = UniqueName("Taken");
        await CreateAsync(client, name);

        var response = await client.PostJsonAsync("/api/views", new SaveViewRequest(name.ToUpperInvariant()));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Overview_CannotBeChangedOrDeleted()
    {
        var (client, _) = await ApiClient.SignedInAsync();

        var put = await client.PutJsonAsync($"/api/views/{View.OverviewId}", new SaveViewRequest(UniqueName("Mine")));
        var delete = await client.DeleteAsync($"/api/views/{View.OverviewId}");

        Assert.Multiple(() =>
        {
            Assert.That(put.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task TheDefaultView_IsPerUser_AndFallsBackToOverviewWhenDeleted()
    {
        var (riley, _) = await ApiClient.SignedInAsync();
        var (sam, _) = await ApiClient.SignedInAsync();
        var view = await CreateAsync(riley, UniqueName("Mine"));

        var before = await (await riley.GetAsync("/api/views/default")).ReadAsync<ViewDto>();
        await riley.PutJsonAsync("/api/views/default", new SetDefaultViewRequest(view.Id));
        var rileys = await (await riley.GetAsync("/api/views/default")).ReadAsync<ViewDto>();
        var sams = await (await sam.GetAsync("/api/views/default")).ReadAsync<ViewDto>();
        await riley.DeleteAsync($"/api/views/{view.Id}");
        var afterDelete = await (await riley.GetAsync("/api/views/default")).ReadAsync<ViewDto>();

        Assert.Multiple(() =>
        {
            Assert.That(before.Id, Is.EqualTo(View.OverviewId));
            Assert.That(rileys.Id, Is.EqualTo(view.Id));
            Assert.That(sams.Id, Is.EqualTo(View.OverviewId));
            Assert.That(afterDelete.Id, Is.EqualTo(View.OverviewId));
        });
    }

    [Test]
    public async Task AReadOnlyAgent_CannotSaveChangeDeleteOrPickADefaultView()
    {
        var (human, _) = await ApiClient.SignedInAsync();
        var view = await CreateAsync(human, UniqueName("Kept"));
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);

        var create = await agent.PostJsonAsync("/api/views", new SaveViewRequest(UniqueName("Agent's")));
        var update = await agent.PutJsonAsync($"/api/views/{view.Id}", new SaveViewRequest(UniqueName("Changed")));
        var delete = await agent.DeleteAsync($"/api/views/{view.Id}");
        var setDefault = await agent.PutJsonAsync("/api/views/default", new SetDefaultViewRequest(view.Id));
        var after = await (await human.GetAsync($"/api/views/{view.Id}")).ReadAsync<ViewDto>();

        Assert.Multiple(() =>
        {
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(setDefault.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(after.Name, Is.EqualTo(view.Name));
        });
    }

    [Test]
    public async Task SignedOut_EveryViewEndpointIsUnauthorized()
    {
        var anonymous = ApiClient.Anonymous();

        var list = await anonymous.GetAsync("/api/views");
        var create = await anonymous.PostJsonAsync("/api/views", new SaveViewRequest(UniqueName("Anyone's")));
        var setDefault = await anonymous.PutJsonAsync("/api/views/default", new SetDefaultViewRequest(View.OverviewId));

        Assert.Multiple(() =>
        {
            Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(setDefault.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        });
    }

    [Test]
    public async Task OverMcp_AReadOnlyAgent_ListsViewsAndRunsOne()
    {
        var (human, _) = await ApiClient.SignedInAsync();
        var home = await ApiClient.CreateSpaceAsync("Home");
        var paint = await human.CreateWorkItemAsync("Paint the hall", spaceId: home);
        await human.CreateWorkItemAsync("Elsewhere", spaceId: await ApiClient.CreateSpaceAsync("Village"));
        var view = await CreateAsync(human, UniqueName("Home only"), home);
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);
        await using var mcp = await ConnectAsync(agent);

        var listed = await mcp.CallToolAsync("list_views", new Dictionary<string, object?>());
        var run = await mcp.CallToolAsync("run_view", new Dictionary<string, object?> { ["viewId"] = view.Id });

        var names = JsonDocument.Parse(TextOf(listed)).RootElement.EnumerateArray()
            .Select(v => McpJson.PropertyOf(v, "name")!.Value.GetString())
            .ToList();
        var items = McpJson.PropertyOf(JsonDocument.Parse(TextOf(run)).RootElement, "items")!.Value.EnumerateArray()
            .Select(i => McpJson.PropertyOf(i, "id")!.Value.GetGuid())
            .ToList();
        Assert.Multiple(() =>
        {
            Assert.That(listed.IsError, Is.Not.True);
            Assert.That(names, Does.Contain("Overview").And.Contain(view.Name));
            Assert.That(run.IsError, Is.Not.True);
            Assert.That(items, Is.EqualTo(new[] { paint.Id }));
        });
    }

    private static async Task<ViewDto> CreateAsync(HttpClient client, string name, params Guid[] spaceIds) =>
        await (await client.PostJsonAsync("/api/views", new SaveViewRequest(name, spaceIds))).ReadAsync<ViewDto>();

    private static string UniqueName(string name) => $"{name} {Guid.NewGuid():N}"[..40];

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
