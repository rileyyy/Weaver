using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Weaver.Api.Contracts;
using Weaver.Domain;
using Weaver.Infrastructure;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// The work-item query over REST and MCP against Postgres, one test per filter: full-text search,
/// tag matching inside the array, the ancestor walk and the cursor run as SQL here, which the
/// InMemory unit tests can't vouch for. Each test uses its own Space, and tags made unique per
/// test, since the database and tags are shared by the whole run.
/// </summary>
[TestFixture]
public class WorkItemSearchTests
{
    private HttpClient _client = null!;
    private AuthResponse _auth = null!;
    private Guid _space;

    [SetUp]
    public async Task SetUp()
    {
        (_client, _auth) = await ApiClient.SignedInAsync();
        _space = await ApiClient.CreateSpaceAsync("Search");
    }

    [Test]
    public async Task Spaces_LimitTheResults()
    {
        var other = await ApiClient.CreateSpaceAsync("Search other");
        var here = await CreateAsync("Here");
        var there = await _client.CreateWorkItemAsync("There", spaceId: other);
        await _client.CreateWorkItemAsync("Elsewhere", spaceId: await ApiClient.CreateSpaceAsync("Search third"));

        var page = await SearchAsync($"spaceIds={_space}&spaceIds={other}");

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { here.Id, there.Id }));
    }

    [Test]
    public async Task Text_UsesFullTextSearch_WithPrefixes_AcrossTitleDescriptionAndWhen()
    {
        var byTitle = await CreateAsync("Paint the hallway");
        var byDescription = await CreateAsync("Decorating");
        await PatchAsync(byDescription, "details", new { description = "Paint every hall door" });
        var byWhen = await CreateAsync("Doors");
        await PatchAsync(byWhen, "details", new { when = "after we paint the hall" });
        await CreateAsync("Paint the shed");

        var page = await SearchAsync($"spaceIds={_space}&text={Uri.EscapeDataString("HALL paint")}");

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { byTitle.Id, byDescription.Id, byWhen.Id }));
    }

    [Test]
    public async Task Text_WithTsQueryOperators_IsTreatedAsWords_NotSyntax()
    {
        var item = await CreateAsync("Hall (upstairs)");

        var page = await SearchAsync($"spaceIds={_space}&text={Uri.EscapeDataString("hall & !(up:* | ')")}");

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { item.Id }));
    }

    [Test]
    public async Task HashNumber_ResolvesDirectly_InEverySpace()
    {
        await CreateAsync("Not this one");
        var wanted = await CreateAsync("This one");

        var page = await SearchAsync($"text={Uri.EscapeDataString($"#{wanted.Number}")}");
        var byNumber = await SearchAsync($"number={wanted.Number}");

        Assert.Multiple(() =>
        {
            Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { wanted.Id }));
            Assert.That(byNumber.Items.Select(i => i.Id), Is.EqualTo(new[] { wanted.Id }));
        });
    }

    [Test]
    public async Task StatusCategories_LimitTheResults()
    {
        var todo = await CreateAsync("Waiting");
        var doing = await CreateAsync("Started");
        var done = await CreateAsync("Finished");
        await MoveToAsync(doing, "Doing");
        await MoveToAsync(done, "Done");

        var page = await SearchAsync($"spaceIds={_space}&statusCategories=ToDo&statusCategories=Doing");

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { todo.Id, doing.Id }));
    }

    [Test]
    public async Task Assignee_LimitsTheResults()
    {
        var mine = await CreateAsync("Mine");
        await CreateAsync("Nobody's");
        (await _client.PutJsonAsync($"/api/work-items/{mine.Id}/assignee", new AssignWorkItemRequest(_auth.User.Id))).EnsureSuccessStatusCode();

        var page = await SearchAsync($"spaceIds={_space}&assigneeId={_auth.User.Id}");

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { mine.Id }));
    }

    [Test]
    public async Task Tags_MatchAnyIgnoringCase_OrAll_AcrossSpaces()
    {
        var outside = UniqueTag("outside");
        var summer = UniqueTag("summer");
        var other = await ApiClient.CreateSpaceAsync("Search tags");
        var justOutside = await CreateAsync("Just outside");
        await TagAsync(justOutside, outside.ToUpperInvariant());
        var both = await _client.CreateWorkItemAsync("Both, elsewhere", spaceId: other);
        await TagAsync(both, outside, summer);
        await TagAsync(await CreateAsync("Untagged"));

        var any = await SearchAsync($"tags={outside}&tags={summer}");
        var all = await SearchAsync($"tags={outside}&tags={summer.ToUpperInvariant()}&tagMatch=All");

        Assert.Multiple(() =>
        {
            Assert.That(any.Items.Select(i => i.Id), Is.EqualTo(new[] { justOutside.Id, both.Id }));
            Assert.That(all.Items.Select(i => i.Id), Is.EqualTo(new[] { both.Id }));
        });
    }

    [Test]
    public async Task Types_LimitTheResults()
    {
        var types = await (await _client.GetAsync($"/api/work-item-types?spaceId={_space}")).ReadAsync<List<WorkItemTypeDto>>();
        var task = types.Single(t => t.Name == "Task").Id;
        var typed = await CreateAsync("Typed");
        await CreateAsync("Untyped");
        await PatchAsync(typed, "details", new { typeId = task });

        var page = await SearchAsync($"typeIds={task}");

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { typed.Id }));
    }

    [Test]
    public async Task DateWindow_IncludesOverlappingItems_AndUndatedAncestorsOfThem_Only()
    {
        var house = await CreateAsync("House");
        var kitchen = await _client.CreateWorkItemAsync("Kitchen", parentId: house.Id);
        var tiles = await _client.CreateWorkItemAsync("Tiles", parentId: kitchen.Id);
        await ScheduleAsync(tiles, "2026-11-10", "2026-11-11");
        var openEnded = await CreateAsync("Started, no end");
        await ScheduleAsync(openEnded, "2026-11-01", null);
        var datedParent = await CreateAsync("Dated parent");
        await ScheduleAsync(datedParent, "2026-12-01", "2026-12-31");
        var child = await _client.CreateWorkItemAsync("Child", parentId: datedParent.Id);
        await ScheduleAsync(child, "2026-11-12", "2026-11-12");
        await CreateAsync("Someday");
        var later = await CreateAsync("Later");
        await ScheduleAsync(later, "2026-11-20", null);

        var page = await SearchAsync($"spaceIds={_space}&from=2026-11-09&to=2026-11-14");

        Assert.That(
            page.Items.Select(i => i.Title),
            Is.EqualTo(new[] { "House", "Kitchen", "Tiles", "Started, no end", "Child" }));
    }

    [Test]
    public async Task DateWindow_IncludesAnUndatedAncestor_OnlyThroughADescendantMatchingTheWholeQuery()
    {
        var tag = UniqueTag("window");
        var garden = await CreateAsync("Garden");
        await TagAsync(garden, tag);
        var hedge = await _client.CreateWorkItemAsync("Hedge, untagged", parentId: garden.Id);
        await ScheduleAsync(hedge, "2026-11-10", "2026-11-11");
        var yard = await CreateAsync("Yard");
        await TagAsync(yard, tag);
        var fence = await _client.CreateWorkItemAsync("Fence", parentId: yard.Id);
        await ScheduleAsync(fence, "2026-11-10", "2026-11-11");
        await TagAsync(fence, tag);

        var page = await SearchAsync($"tags={tag}&from=2026-11-09&to=2026-11-14");

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { yard.Id, fence.Id }));
    }

    [Test]
    public async Task BacklogOnly_ListsUndatedItemsThatAreNotDone()
    {
        var someday = await CreateAsync("Someday");
        var done = await CreateAsync("Done");
        await MoveToAsync(done, "Done");
        var dated = await CreateAsync("Dated");
        await ScheduleAsync(dated, null, "2026-11-10");

        var page = await SearchAsync($"spaceIds={_space}&backlogOnly=true");

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { someday.Id }));
    }

    [Test]
    public async Task UpdatedSince_LimitsTheResults_WhateverTheOffset()
    {
        var old = await CreateAsync("Old");
        var since = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(10));
        var touched = await CreateAsync("Touched");
        await PatchAsync(touched, "details", new { title = "Touched again" });

        var page = await SearchAsync($"spaceIds={_space}&updatedSince={Uri.EscapeDataString(since.ToString("O"))}");

        Assert.Multiple(() =>
        {
            Assert.That(page.Items.Select(i => i.Id), Does.Not.Contain(old.Id));
            Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { touched.Id }));
        });
    }

    [Test]
    public async Task Filters_AndTogether()
    {
        var tag = UniqueTag("fence");
        var match = await CreateAsync("Paint the fence");
        await TagAsync(match, tag);
        var wrongText = await CreateAsync("Mend the fence");
        await TagAsync(wrongText, tag);
        var wrongStatus = await CreateAsync("Paint the fence again");
        await TagAsync(wrongStatus, tag);
        await MoveToAsync(wrongStatus, "Done");
        await CreateAsync("Paint the fence, untagged");

        var page = await SearchAsync($"spaceIds={_space}&text=paint&tags={tag}&statusCategories=ToDo");

        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { match.Id }));
    }

    [Test]
    public async Task Paging_NeitherSkipsNorRepeats_WhenRanksChangeBetweenPages()
    {
        var created = new List<WorkItemDto>();
        foreach (var title in new[] { "A", "B", "C", "D", "E" })
        {
            created.Add(await CreateAsync(title));
        }

        var first = await SearchAsync($"spaceIds={_space}&limit=2");
        // Each move puts the item first in its new cell, reshuffling the rank order.
        await MoveToAsync(created[4], "Doing");
        await MoveToAsync(created[0], "Doing");
        await MoveToAsync(created[4], "To Do");
        var late = await CreateAsync("F");
        var second = await SearchAsync($"spaceIds={_space}&limit=2&cursor={Uri.EscapeDataString(first.NextCursor!)}");
        var third = await SearchAsync($"spaceIds={_space}&limit=2&cursor={Uri.EscapeDataString(second.NextCursor!)}");

        Assert.Multiple(() =>
        {
            Assert.That(
                first.Items.Concat(second.Items).Concat(third.Items).Select(i => i.Id),
                Is.EqualTo(created.Select(c => c.Id).Append(late.Id)));
            Assert.That(third.NextCursor, Is.Null);
        });
    }

    [Test]
    public async Task BadArguments_Return400()
    {
        var badCursor = await _client.GetAsync("/api/work-items/search?cursor=garbage");
        var badLimit = await _client.GetAsync("/api/work-items/search?limit=0");
        var badWindow = await _client.GetAsync("/api/work-items/search?from=2026-11-14&to=2026-11-09");

        Assert.Multiple(() =>
        {
            Assert.That(badCursor.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
            Assert.That(badLimit.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
            Assert.That(badWindow.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
        });
    }

    // With sequential scans off, Postgres picks an index whenever it can use one, so this shows
    // the search's SQL matches the GIN index's expression however small the table is.
    [Test]
    public async Task TextSearch_CanUseTheFullTextIndex()
    {
        using var scope = IntegrationTestEnvironment.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaverDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SET LOCAL enable_seqscan = off");

        await using var explain = new PostgresWorkItemSearch().MatchingText(db.WorkItems, "hall").CreateDbCommand();
        explain.CommandText = "EXPLAIN " + explain.CommandText;
        explain.Transaction = transaction.GetDbTransaction();
        var plan = await ReadAllAsync(explain);

        Assert.That(plan, Does.Contain(PostgresWorkItemSearch.IndexName));
    }

    [Test]
    public async Task Mcp_SearchWorkItems_FindsByHashNumber_WithAReadOnlyKey()
    {
        var item = await CreateAsync("Seen by an agent");
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);
        await using var mcp = await ConnectAsync(agent);

        var result = await mcp.CallToolAsync("search_work_items", new Dictionary<string, object?>
        {
            ["text"] = $"#{item.Number}",
        });

        var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
        Assert.That(result.IsError, Is.Not.True, text);
        var page = JsonSerializer.Deserialize<WorkItemSummaryPageDto>(text, ApiClient.Json)!;
        Assert.Multiple(() =>
        {
            Assert.That(page.Items.Single().Id, Is.EqualTo(item.Id));
            Assert.That(page.Items.Single().StatusName, Is.EqualTo("To Do"));
            Assert.That(page.NextCursor, Is.Null);
        });
    }

    [Test]
    public async Task Mcp_SearchWorkItems_TakesListAndEnumArguments()
    {
        var tag = UniqueTag("agent");
        var item = await CreateAsync("Tagged for an agent");
        await TagAsync(item, tag);
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);
        await using var mcp = await ConnectAsync(agent);

        var result = await mcp.CallToolAsync("search_work_items", new Dictionary<string, object?>
        {
            ["spaceIds"] = new[] { _space },
            ["statusCategories"] = new[] { "ToDo" },
            ["tags"] = new[] { tag },
            ["tagMatch"] = "All",
        });

        var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
        Assert.That(result.IsError, Is.Not.True, text);
        var page = JsonSerializer.Deserialize<WorkItemSummaryPageDto>(text, ApiClient.Json)!;
        Assert.That(page.Items.Select(i => i.Id), Is.EqualTo(new[] { item.Id }));
    }

    private Task<WorkItemDto> CreateAsync(string title) => _client.CreateWorkItemAsync(title, spaceId: _space);

    private async Task<WorkItemPageDto> SearchAsync(string query) =>
        await (await _client.GetAsync($"/api/work-items/search?{query}")).ReadAsync<WorkItemPageDto>();

    private async Task PatchAsync<T>(WorkItemDto item, string endpoint, T body) =>
        (await _client.PatchJsonAsync($"/api/work-items/{item.Id}/{endpoint}", body)).EnsureSuccessStatusCode();

    private Task ScheduleAsync(WorkItemDto item, string? start, string? end) =>
        PatchAsync(item, "schedule", new { startDate = start, endDate = end });

    private Task TagAsync(WorkItemDto item, params string[] tags) => PatchAsync(item, "tags", new { tags });

    private async Task MoveToAsync(WorkItemDto item, string status)
    {
        var statusId = await ApiClient.StatusIdAsync(item.SpaceId, status);
        (await _client.PutJsonAsync($"/api/work-items/{item.Id}/status", new ChangeWorkItemStatusRequest(statusId, null))).EnsureSuccessStatusCode();
    }

    private static string UniqueTag(string name) => $"{name}-{Guid.NewGuid():N}"[..20];

    private static async Task<string> ReadAllAsync(DbCommand command)
    {
        var lines = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lines.Add(reader.GetString(0));
        }

        return string.Join("\n", lines);
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
