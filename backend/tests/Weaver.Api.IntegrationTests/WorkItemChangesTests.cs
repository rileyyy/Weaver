using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Weaver.Api.Contracts;
using Weaver.Domain;
using Weaver.Infrastructure;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// "Changes since T" over REST and MCP against Postgres: the grouping, cursor and filters run
/// as SQL here, which the InMemory unit tests can't vouch for. Each test uses its own Space,
/// since the database is shared by the whole run.
/// </summary>
[TestFixture]
public class WorkItemChangesTests
{
    [Test]
    public async Task FieldChangesAndDeletes_AreListed_WithTheEntriesSinceT()
    {
        var (client, auth) = await ApiClient.SignedInAsync();
        var space = await ApiClient.CreateSpaceAsync("Changes");
        var kept = await client.CreateWorkItemAsync("Boiler", spaceId: space);
        var gone = await client.CreateWorkItemAsync("Old boiler", spaceId: space);
        var since = DateTimeOffset.UtcNow;

        (await client.PutJsonAsync($"/api/work-items/{kept.Id}/status", new ChangeWorkItemStatusRequest(SeededStatuses.Doing, null))).EnsureSuccessStatusCode();
        (await client.DeleteAsync($"/api/work-items/{gone.Id}")).EnsureSuccessStatusCode();

        var page = await ChangesAsync(client, since, space);

        Assert.Multiple(() =>
        {
            Assert.That(page.Items.Select(i => (i.Item.Id, i.Deleted)), Is.EqualTo(new[] { (kept.Id, false), (gone.Id, true) }));
            Assert.That(page.Items[0].Item.Number, Is.EqualTo(kept.Number));
            Assert.That(page.Items[0].Item.StatusName, Is.EqualTo("Doing"));
            Assert.That(page.Items[0].Changes.Select(c => c.Field), Is.EqualTo(new[] { "statusId" }));
            Assert.That(page.Items[0].Changes[0].AccountName, Is.EqualTo(auth.User.Username));
            Assert.That(page.Items[1].Item.Title, Is.EqualTo("Old boiler"));
            Assert.That(page.Items[1].Item.SpaceId, Is.EqualTo(space));
            Assert.That(page.Items[1].Changes.Select(c => c.Field), Is.EqualTo(new[] { "deleted" }));
            Assert.That(page.NextCursor, Is.Null);
        });
    }

    [Test]
    public async Task Paging_WalksEveryItemOnce_AndIsStable_WhenChangesLandBetweenPages()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var space = await ApiClient.CreateSpaceAsync("Paging");
        var since = DateTimeOffset.UtcNow;
        var created = new List<WorkItemDto>();
        foreach (var title in new[] { "A", "B", "C", "D", "E" })
        {
            created.Add(await client.CreateWorkItemAsync(title, spaceId: space));
        }

        var first = await ChangesAsync(client, since, space, limit: 2);
        (await client.PatchJsonAsync($"/api/work-items/{created[0].Id}/details", new { title = "A again" })).EnsureSuccessStatusCode();
        var late = await client.CreateWorkItemAsync("F", spaceId: space);
        var second = await ChangesAsync(client, since, space, first.NextCursor, limit: 2);
        var third = await ChangesAsync(client, since, space, second.NextCursor, limit: 2);

        Assert.Multiple(() =>
        {
            Assert.That(first.Items.Select(i => i.Item.Title), Is.EqualTo(new[] { "A", "B" }));
            Assert.That(second.Items.Select(i => i.Item.Title), Is.EqualTo(new[] { "C", "D" }));
            Assert.That(third.Items.Select(i => i.Item.Id), Is.EqualTo(new[] { created[4].Id, late.Id }));
            Assert.That(third.NextCursor, Is.Null);
        });
    }

    [Test]
    public async Task ExcludingOwnChanges_KeepsOtherPeoplesAndWeaversEntries()
    {
        var (me, _) = await ApiClient.SignedInAsync();
        var (someoneElse, other) = await ApiClient.SignedInAsync();
        var space = await ApiClient.CreateSpaceAsync("Exclude own");
        var mine = await me.CreateWorkItemAsync("Mine", spaceId: space);
        var theirs = await me.CreateWorkItemAsync("Theirs", spaceId: space);
        var generated = await me.CreateWorkItemAsync("Generated", spaceId: space);
        var since = DateTimeOffset.UtcNow;

        (await me.PatchJsonAsync($"/api/work-items/{mine.Id}/details", new { title = "Mine again" })).EnsureSuccessStatusCode();
        (await someoneElse.PatchJsonAsync($"/api/work-items/{theirs.Id}/details", new { title = "Theirs again" })).EnsureSuccessStatusCode();
        await RecordAsWeaverAsync(generated.Id);

        var page = await ChangesAsync(me, since, space, excludeOwnChanges: true);

        Assert.Multiple(() =>
        {
            Assert.That(page.Items.Select(i => i.Item.Id), Is.EqualTo(new[] { theirs.Id, generated.Id }));
            Assert.That(page.Items[0].Changes.Single().AccountName, Is.EqualTo(other.User.Username));
            Assert.That(page.Items[1].Changes.Single().AccountId, Is.Null);
        });
    }

    // Npgsql refuses a DateTimeOffset parameter with a non-zero offset, and agents send local times.
    [Test]
    public async Task ASinceWithAnOffset_MeansTheSameInstant()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var space = await ApiClient.CreateSpaceAsync("Offset");
        var since = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(10));
        var item = await client.CreateWorkItemAsync("Down under", spaceId: space);

        var page = await ChangesAsync(client, since, space);

        Assert.That(page.Items.Select(i => i.Item.Id), Is.EqualTo(new[] { item.Id }));
    }

    [Test]
    public async Task BadArguments_Return400()
    {
        var (client, _) = await ApiClient.SignedInAsync();

        var noSince = await client.GetAsync("/api/work-items/changes");
        var badCursor = await client.GetAsync($"/api/work-items/changes?since={Since(DateTimeOffset.UtcNow)}&cursor=garbage");
        var badLimit = await client.GetAsync($"/api/work-items/changes?since={Since(DateTimeOffset.UtcNow)}&limit=0");

        Assert.Multiple(() =>
        {
            Assert.That(noSince.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(badCursor.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(badLimit.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task Mcp_ListChangesSince_WorksWithAReadOnlyKey()
    {
        var (writer, _) = await ApiClient.SignedInAsync();
        var space = await ApiClient.CreateSpaceAsync("MCP changes");
        var since = DateTimeOffset.UtcNow;
        var item = await writer.CreateWorkItemAsync("Seen by an agent", spaceId: space);
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);
        await using var mcp = await ConnectAsync(agent);

        var result = await mcp.CallToolAsync("list_changes_since", new Dictionary<string, object?>
        {
            ["since"] = since,
            ["spaceId"] = space,
        });

        var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
        Assert.That(result.IsError, Is.Not.True, text);
        var page = JsonSerializer.Deserialize<WorkItemChangesPageDto>(text, ApiClient.Json)!;
        Assert.Multiple(() =>
        {
            Assert.That(page.Items.Single().Item.Id, Is.EqualTo(item.Id));
            Assert.That(page.Items.Single().Changes.Select(c => c.Field), Is.EqualTo(new[] { "created", "spaceId" }));
        });
    }

    /// <summary>
    /// Stands in for <c>RecurrenceGenerationWorker</c>, which records as Weaver itself, without
    /// waiting for its schedule.
    /// </summary>
    private static async Task RecordAsWeaverAsync(Guid workItemId)
    {
        using var scope = IntegrationTestEnvironment.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaverDbContext>();
        db.WorkItemHistory.Add(WorkItemHistoryEntry.Record(workItemId, HistoryActor.System, "startDate", null, "\"2026-11-09\""));
        await db.SaveChangesAsync();
    }

    private static async Task<WorkItemChangesPageDto> ChangesAsync(
        HttpClient client,
        DateTimeOffset since,
        Guid spaceId,
        string? cursor = null,
        int limit = 50,
        bool excludeOwnChanges = false)
    {
        var query = $"since={Since(since)}&spaceId={spaceId}&limit={limit}&excludeOwnChanges={excludeOwnChanges}";
        if (cursor is not null)
        {
            query += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return await (await client.GetAsync($"/api/work-items/changes?{query}")).ReadAsync<WorkItemChangesPageDto>();
    }

    private static string Since(DateTimeOffset since) => Uri.EscapeDataString(since.ToString("O"));

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
