using System.Net;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Weaver.Api.Contracts;
using Weaver.Api.Mcp;
using Weaver.Domain;
using static Weaver.Api.IntegrationTests.McpJson;

namespace Weaver.Api.IntegrationTests;

[TestFixture]
public class McpTests
{
    [Test]
    public async Task Mcp_WithoutToken_Returns401()
    {
        var response = await ApiClient.Anonymous().PostAsync("/mcp", new StringContent("{}"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Mcp_WithToken_ListsAndCallsTools()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        await using var mcp = await ConnectAsync(client);

        var tools = await mcp.ListToolsAsync();
        var result = await mcp.CallToolAsync(
            "list_statuses",
            new Dictionary<string, object?> { ["spaceId"] = await ApiClient.SharedSpaceIdAsync() });
        var allItems = await mcp.CallToolAsync("list_all_work_items", new Dictionary<string, object?>());

        Assert.Multiple(() =>
        {
            Assert.That(tools.Select(t => t.Name), Does.Contain("create_work_item").And.Contain("list_all_work_items"));
            Assert.That(result.IsError, Is.Not.True);
            Assert.That(allItems.IsError, Is.Not.True);
        });
    }

    [Test]
    public async Task Mcp_ListWorkItemChildren_ReturnsSummaries_WithoutNulls_WhileRestKeepsThem()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var parent = await client.CreateWorkItemAsync($"Parent {Guid.NewGuid():N}");
        var child = await client.CreateWorkItemAsync("Undated child", parent.Id);
        await using var mcp = await ConnectAsync(client);

        var result = await mcp.CallToolAsync(
            "list_work_item_children",
            new Dictionary<string, object?> { ["parentId"] = parent.Id });
        var rest = await (await client.GetAsync($"/api/work-items/{child.Id}")).Content.ReadAsStringAsync();

        Assert.That(result.IsError, Is.Not.True, () => TextOf(result));
        var summary = JsonDocument.Parse(TextOf(result)).RootElement.EnumerateArray().Single();
        Assert.Multiple(() =>
        {
            Assert.That(PropertyOf(summary, "id")?.GetGuid(), Is.EqualTo(child.Id));
            Assert.That(PropertyOf(summary, "parentId")?.GetGuid(), Is.EqualTo(parent.Id));
            Assert.That(PropertyOf(summary, "statusName")?.GetString(), Is.EqualTo("To Do"));
            Assert.That(PropertyOf(summary, "childCount")?.GetInt32(), Is.EqualTo(0));
            Assert.That(PropertyOf(summary, "statusId"), Is.Null, "a summary names the status instead");
            Assert.That(PropertyOf(summary, "startDate"), Is.Null, "nulls are left out over MCP");
            Assert.That(PropertyOf(summary, "assigneeName"), Is.Null, "nulls are left out over MCP");
            Assert.That(rest, Does.Contain("\"startDate\":null"), "REST JSON still writes nulls");
        });
    }

    [Test]
    public async Task Mcp_GetWorkItem_ByNumber_ReturnsTheFullItemWithNames()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var item = await client.CreateWorkItemAsync($"Known as a number {Guid.NewGuid():N}");
        await using var mcp = await ConnectAsync(client);

        var result = await mcp.CallToolAsync("get_work_item", new Dictionary<string, object?> { ["number"] = item.Number });
        var neither = await mcp.CallToolAsync("get_work_item", new Dictionary<string, object?>());

        Assert.That(result.IsError, Is.Not.True, () => TextOf(result));
        var found = JsonSerializer.Deserialize<WorkItemDto>(TextOf(result), ApiClient.Json)!;
        Assert.Multiple(() =>
        {
            Assert.That(found.Id, Is.EqualTo(item.Id));
            Assert.That(found.StatusId, Is.EqualTo(item.StatusId));
            Assert.That(found.StatusName, Is.Not.Null.And.EqualTo(item.StatusName));
            Assert.That(found.SpaceName, Is.Not.Null.And.EqualTo(item.SpaceName));
            Assert.That(neither.IsError, Is.True);
        });
    }

    [Test]
    public async Task Mcp_GetWorkItemContext_ByNumber_ReturnsOnlyTheIncludes_AndLeavesTheTrashOut()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var root = await client.CreateWorkItemAsync($"Kitchen {Guid.NewGuid():N}");
        var item = await client.CreateWorkItemAsync("Cabinets", root.Id);
        var child = await client.CreateWorkItemAsync("Hinges", item.Id);
        var trashedChild = await client.CreateWorkItemAsync("Old handles", item.Id);
        var linked = await client.CreateWorkItemAsync($"Drywall {Guid.NewGuid():N}");
        var trashedLinked = await client.CreateWorkItemAsync($"Old quote {Guid.NewGuid():N}");
        await client.PostJsonAsync($"/api/work-items/{item.Id}/links", new CreateWorkItemLinkRequest(linked.Id, WorkItemLinkRelation.BlockedBy));
        await client.PostJsonAsync($"/api/work-items/{item.Id}/links", new CreateWorkItemLinkRequest(trashedLinked.Id, WorkItemLinkRelation.Related));
        await client.PostJsonAsync($"/api/work-items/{item.Id}/comments", new CreateCommentRequest("Ordered"));
        (await client.DeleteAsync($"/api/work-items/{trashedChild.Id}")).EnsureSuccessStatusCode();
        (await client.DeleteAsync($"/api/work-items/{trashedLinked.Id}")).EnsureSuccessStatusCode();
        await using var mcp = await ConnectAsync(client);

        var result = await mcp.CallToolAsync("get_work_item_context", new Dictionary<string, object?>
        {
            ["number"] = item.Number,
            ["include"] = new[] { "ancestors", "children", "comments", "links", "history" },
            ["historyLimit"] = 1,
        });
        var unknown = await mcp.CallToolAsync("get_work_item_context", new Dictionary<string, object?>
        {
            ["id"] = item.Id,
            ["include"] = new[] { "parents" },
        });

        Assert.That(result.IsError, Is.Not.True, () => TextOf(result));
        var context = JsonDocument.Parse(TextOf(result)).RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(PropertyOf(PropertyOf(context, "item")!.Value, "id")?.GetGuid(), Is.EqualTo(item.Id));
            Assert.That(IdsIn(context, "ancestors"), Is.EqualTo(new[] { root.Id }));
            Assert.That(IdsIn(context, "children"), Is.EqualTo(new[] { child.Id }));
            Assert.That(PropertyOf(context, "comments")?.GetArrayLength(), Is.EqualTo(1));
            Assert.That(
                PropertyOf(context, "links")!.Value.EnumerateArray().Select(l => PropertyOf(l, "linkedWorkItemId")?.GetGuid()),
                Is.EqualTo(new Guid?[] { linked.Id }));
            Assert.That(PropertyOf(context, "history")?.GetArrayLength(), Is.EqualTo(1));
            Assert.That(PropertyOf(context, "nextHistoryOffset")?.GetInt32(), Is.EqualTo(1));
            Assert.That(PropertyOf(context, "attachments"), Is.Null, "attachments weren't asked for");
            Assert.That(unknown.IsError, Is.True);
            Assert.That(TextOf(unknown), Does.Contain("ancestors, children, comments, links, history, attachments"));
        });
    }

    private static IEnumerable<Guid?> IdsIn(JsonElement obj, string listName) =>
        PropertyOf(obj, listName)!.Value.EnumerateArray().Select(e => PropertyOf(e, "id")?.GetGuid());

    // Agents may read the server settings but never change them (ADR-0004).
    [Test]
    public async Task Mcp_ServerSettings_AreReadableButNotWritable()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        await using var mcp = await ConnectAsync(client);

        var tools = await mcp.ListToolsAsync();
        var result = await mcp.CallToolAsync("get_server_settings", new Dictionary<string, object?>());

        var settingsTools = tools.Select(t => t.Name).Where(n => n.Contains("setting") || n.Contains("time_zone") || n.Contains("retention"));
        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.Not.True);
            Assert.That(settingsTools, Is.EqualTo(new[] { "get_server_settings" }));
        });
    }

    [Test]
    public async Task Mcp_WithAnApiKey_WhoamiNamesTheAgentTheKeyAndItsScope()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var agentName = $"Claude Code ({Guid.NewGuid():N})"[..40];
        var agent = await (await admin.PostJsonAsync("/api/admin/agents", new CreateAgentAccountRequest(agentName, null)))
            .ReadAsync<AgentAccountDto>();
        var issued = await (await admin.PostJsonAsync(
                $"/api/admin/agents/{agent.Id}/keys",
                new CreateApiKeyRequest("laptop", ApiKeyScope.ReadOnly)))
            .ReadAsync<IssuedApiKeyDto>();
        await using var mcp = await ConnectAsync(ApiClient.WithApiKey(issued.Key));

        var whoami = await CallAsync<WhoAmIDto>(mcp, "whoami");

        Assert.That(whoami, Is.EqualTo(new WhoAmIDto(agentName, UserKind.Agent, "laptop", ApiKeyScope.ReadOnly)));
    }

    [Test]
    public async Task Mcp_WithAnAccessToken_WhoamiNamesTheHumanAndNoKey()
    {
        var (client, auth) = await ApiClient.SignedInAsync();
        await using var mcp = await ConnectAsync(client);

        var whoami = await CallAsync<WhoAmIDto>(mcp, "whoami");

        Assert.That(whoami, Is.EqualTo(new WhoAmIDto(auth.User.Username, UserKind.Human, KeyLabel: null, Scope: null)));
    }

    [Test]
    public async Task Mcp_WithARevokedKey_Returns401()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var agent = await (await admin.PostJsonAsync(
                "/api/admin/agents",
                new CreateAgentAccountRequest($"Revoked ({Guid.NewGuid():N})"[..40], null)))
            .ReadAsync<AgentAccountDto>();
        var issued = await (await admin.PostJsonAsync(
                $"/api/admin/agents/{agent.Id}/keys",
                new CreateApiKeyRequest("laptop", ApiKeyScope.ReadWrite)))
            .ReadAsync<IssuedApiKeyDto>();
        await admin.DeleteAsync($"/api/admin/agents/{agent.Id}/keys/{issued.ApiKey.Id}");

        var response = await ApiClient.WithApiKey(issued.Key).PostAsync("/mcp", new StringContent("{}"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Mcp_WithAReadOnlyKey_WriteToolsAreRefused_AndNothingChanges()
    {
        var (human, _) = await ApiClient.SignedInAsync();
        var item = await human.CreateWorkItemAsync("Leave me alone");
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);
        await using var mcp = await ConnectAsync(agent);

        var create = await mcp.CallToolAsync("create_work_item", new Dictionary<string, object?>
        {
            ["title"] = "Not allowed",
            ["statusId"] = await ApiClient.StatusIdAsync(await ApiClient.SharedSpaceIdAsync(), "To Do"),
            ["spaceId"] = await ApiClient.SharedSpaceIdAsync(),
        });
        var delete = await mcp.CallToolAsync("delete_work_item", new Dictionary<string, object?> { ["id"] = item.Id });
        var stillThere = await human.GetAsync($"/api/work-items/{item.Id}");

        Assert.Multiple(() =>
        {
            Assert.That(create.IsError, Is.True);
            Assert.That(TextOf(create), Does.Contain("read-only"));
            Assert.That(delete.IsError, Is.True);
            Assert.That(TextOf(delete), Does.Contain("read-only"));
            Assert.That(stillThere.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task Mcp_WithAReadOnlyKey_ReadToolsStillWork()
    {
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);
        await using var mcp = await ConnectAsync(agent);

        var items = await mcp.CallToolAsync("list_all_work_items", new Dictionary<string, object?>());
        var settings = await mcp.CallToolAsync("get_server_settings", new Dictionary<string, object?>());

        Assert.Multiple(() =>
        {
            Assert.That(items.IsError, Is.Not.True, () => TextOf(items));
            Assert.That(settings.IsError, Is.Not.True, () => TextOf(settings));
        });
    }

    // ADR-0007: a read-only key sees the Trash but can't restore from it.
    [Test]
    public async Task Mcp_WithAReadOnlyKey_ListsTheTrash_ButCantRestore()
    {
        var (human, _) = await ApiClient.SignedInAsync();
        var item = await human.CreateWorkItemAsync($"Trashed {Guid.NewGuid():N}");
        await human.DeleteAsync($"/api/work-items/{item.Id}");
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);
        await using var mcp = await ConnectAsync(agent);

        var trash = await CallAsync<List<TrashedWorkItemDto>>(mcp, "list_trash");
        var restore = await mcp.CallToolAsync("restore_work_item", new Dictionary<string, object?> { ["id"] = item.Id });

        Assert.Multiple(() =>
        {
            Assert.That(trash.Select(t => t.Item.Id), Does.Contain(item.Id));
            Assert.That(restore.IsError, Is.True);
            Assert.That(TextOf(restore), Does.Contain("read-only"));
        });
    }

    [Test]
    public async Task Mcp_WithAReadWriteKey_RestoresFromTheTrash()
    {
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);
        var item = await agent.CreateWorkItemAsync($"Trashed {Guid.NewGuid():N}");
        await using var mcp = await ConnectAsync(agent);
        await mcp.CallToolAsync("delete_work_item", new Dictionary<string, object?> { ["id"] = item.Id });

        var restore = await mcp.CallToolAsync("restore_work_item", new Dictionary<string, object?> { ["id"] = item.Id });
        var after = await agent.GetAsync($"/api/work-items/{item.Id}");

        Assert.Multiple(() =>
        {
            Assert.That(restore.IsError, Is.Not.True, () => TextOf(restore));
            Assert.That(after.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task Mcp_WithAReadWriteKey_WriteToolsWork()
    {
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);
        await using var mcp = await ConnectAsync(agent);

        var create = await mcp.CallToolAsync("create_work_item", new Dictionary<string, object?>
        {
            ["title"] = "Written over MCP",
            ["statusId"] = await ApiClient.StatusIdAsync(await ApiClient.SharedSpaceIdAsync(), "To Do"),
            ["spaceId"] = await ApiClient.SharedSpaceIdAsync(),
        });
        var created = JsonSerializer.Deserialize<WorkItemDto>(TextOf(create), ApiClient.Json)!;
        var delete = await mcp.CallToolAsync("delete_work_item", new Dictionary<string, object?> { ["id"] = created.Id });

        Assert.Multiple(() =>
        {
            Assert.That(create.IsError, Is.Not.True, () => TextOf(create));
            Assert.That(created.Title, Is.EqualTo("Written over MCP"));
            Assert.That(delete.IsError, Is.Not.True, () => TextOf(delete));
        });
    }

    [Test]
    public async Task Mcp_ListDependencyConflicts_NamesTheBlockedItemAndItsBlocker_AndRestFlagsTheLink()
    {
        var (human, _) = await ApiClient.SignedInAsync();
        var spaceId = await ApiClient.CreateSpaceAsync("Conflicts");
        var drywall = await human.CreateWorkItemAsync("Drywall", spaceId: spaceId);
        var paint = await human.CreateWorkItemAsync("Paint", spaceId: spaceId);
        var trim = await human.CreateWorkItemAsync("Trim", spaceId: spaceId);
        await ScheduleAsync(human, drywall, "2026-11-02", "2026-11-06");
        await ScheduleAsync(human, paint, "2026-11-05", "2026-11-08");
        await ScheduleAsync(human, trim, "2026-11-06", "2026-11-09");
        await human.PostJsonAsync($"/api/work-items/{paint.Id}/links", new CreateWorkItemLinkRequest(drywall.Id, WorkItemLinkRelation.BlockedBy));
        await human.PostJsonAsync($"/api/work-items/{trim.Id}/links", new CreateWorkItemLinkRequest(drywall.Id, WorkItemLinkRelation.BlockedBy));
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);
        await using var mcp = await ConnectAsync(agent);

        var result = await mcp.CallToolAsync("list_dependency_conflicts", new Dictionary<string, object?> { ["spaceId"] = spaceId });
        var paintOverRest = await (await human.GetAsync($"/api/work-items/{paint.Id}")).ReadAsync<WorkItemDto>();
        var trimOverRest = await (await human.GetAsync($"/api/work-items/{trim.Id}")).ReadAsync<WorkItemDto>();

        Assert.That(result.IsError, Is.Not.True, () => TextOf(result));
        var conflicts = JsonSerializer.Deserialize<List<DependencyConflictDto>>(TextOf(result), ApiClient.Json)!;
        Assert.Multiple(() =>
        {
            Assert.That(conflicts, Has.Count.EqualTo(1), "trim starts on drywall's end date, which isn't a conflict");
            Assert.That(conflicts[0].Blocked.Id, Is.EqualTo(paint.Id));
            Assert.That(conflicts[0].Blocked.StartDate, Is.EqualTo(new DateOnly(2026, 11, 5)));
            Assert.That(conflicts[0].Blocker.Id, Is.EqualTo(drywall.Id));
            Assert.That(conflicts[0].Blocker.EndDate, Is.EqualTo(new DateOnly(2026, 11, 6)));
            Assert.That(conflicts[0].Blocker.SpaceName, Does.StartWith("Conflicts"));
            Assert.That(paintOverRest.BlockedBy.Select(b => (b.Id, b.Conflicts)), Is.EqualTo(new[] { (drywall.Id, true) }));
            Assert.That(trimOverRest.BlockedBy.Select(b => (b.Id, b.Conflicts)), Is.EqualTo(new[] { (drywall.Id, false) }));
        });
    }

    // Marking a tool ReadOnly is what lets a read-only key call it, so a write tool marked that
    // way would be a hole. Tools are named for what they do, which makes the mistake visible.
    // plan_ is a dry run: it reports what a write would do without making it. run_view runs a
    // saved query; it's named alone rather than allowing run_, which could hide a write later.
    [Test]
    public async Task Mcp_OnlyReadingToolsAreMarkedReadOnly()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        await using var mcp = await ConnectAsync(client);

        var tools = await mcp.ListToolsAsync();
        var readOnly = tools.Where(t => t.ProtocolTool.Annotations?.ReadOnlyHint == true).Select(t => t.Name).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(readOnly, Is.Not.Empty);
            Assert.That(
                readOnly,
                Has.All.Matches<string>(n =>
                    n.StartsWith("list_") || n.StartsWith("get_") || n.StartsWith("search_") || n.StartsWith("plan_")
                    || n == "whoami" || n == "run_view"));
        });
    }

    // Agents never change admin data (ADR-0004). AdminAuthorizer refuses them anyway, but no MCP
    // tool should offer the change: every write is on work items or their comments.
    [Test]
    public async Task Mcp_OffersAReadWriteAgentNoAdminWriteTools()
    {
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);
        await using var mcp = await ConnectAsync(agent);

        var tools = await mcp.ListToolsAsync();
        var writes = tools.Where(t => t.ProtocolTool.Annotations?.ReadOnlyHint != true).Select(t => t.Name).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(writes, Does.Contain("create_work_item"));
            Assert.That(
                writes,
                Has.All.Matches<string>(n => n.Contains("work_item") || n.EndsWith("_comment")));
        });
    }

    // Agents can't purge (ADR-0007); over MCP there's simply no tool for it.
    [Test]
    public async Task Mcp_OffersAReadWriteAgentNoWayToPurgeTheTrash()
    {
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);
        await using var mcp = await ConnectAsync(agent);

        var names = (await mcp.ListToolsAsync()).Select(t => t.Name).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Contain("restore_work_item"));
            Assert.That(names, Has.None.Contains("purge").And.None.Contains("empty"));
        });
    }

    // A read-only key must still reach the guidance: an agent reads it before it knows what it may do.
    [Test]
    public async Task Mcp_SendsTheInstructions_AndServesTheReference_EvenToAReadOnlyKey()
    {
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);
        await using var mcp = await ConnectAsync(agent);

        var resources = await mcp.ListResourcesAsync();
        var reference = await mcp.ReadResourceAsync(ReferenceResource.Uri);

        Assert.Multiple(() =>
        {
            Assert.That(mcp.ServerInstructions, Is.EqualTo(AgentGuidance.ServerInstructions));
            Assert.That(resources.Select(r => r.Uri), Does.Contain(ReferenceResource.Uri));
            Assert.That(
                reference.Contents.OfType<TextResourceContents>().Single().Text,
                Is.EqualTo(AgentGuidance.Reference));
        });
    }

    private static string TextOf(CallToolResult result) =>
        string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    private static async Task<T> CallAsync<T>(McpClient mcp, string tool)
    {
        var result = await mcp.CallToolAsync(tool, new Dictionary<string, object?>());
        Assert.That(result.IsError, Is.Not.True, () => string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text)));
        return JsonSerializer.Deserialize<T>(result.Content.OfType<TextContentBlock>().Single().Text, ApiClient.Json)!;
    }

    private static async Task ScheduleAsync(HttpClient client, WorkItemDto item, string startDate, string endDate) =>
        (await client.PatchJsonAsync($"/api/work-items/{item.Id}/schedule", new { startDate, endDate })).EnsureSuccessStatusCode();

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
