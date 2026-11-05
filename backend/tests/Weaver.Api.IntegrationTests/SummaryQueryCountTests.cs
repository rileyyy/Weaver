using Microsoft.EntityFrameworkCore;
using Weaver.Api.Contracts;
using Weaver.Application.Services;
using Weaver.Infrastructure;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// Summaries carry names and a child count so an agent needs no lookup per item; that must not
/// turn into a query per item on the server either. Counted on Postgres, since InMemory sends no
/// SQL to count.
/// </summary>
[TestFixture]
public class SummaryQueryCountTests
{
    private HttpClient _client = null!;
    private Guid _userId;
    private string _username = null!;

    [SetUp]
    public async Task SetUp()
    {
        var (client, auth) = await ApiClient.SignedInAsync();
        _client = client;
        _userId = auth.User.Id;
        _username = auth.User.Username;
    }

    [TestCase(1)]
    [TestCase(6)]
    public async Task ChildSummaries_AreOneQuery_HoweverManyChildren(int children)
    {
        var parent = await CreateTreeAsync(children);
        var counter = new CommandCounter();
        await using var db = CountingContext(counter);

        var summaries = await WorkItems(db).GetChildSummariesAsync(parent.Id);

        Assert.Multiple(() =>
        {
            Assert.That(summaries, Has.Count.EqualTo(children));
            Assert.That(summaries.Select(s => s.AssigneeName), Has.All.EqualTo(_username));
            Assert.That(summaries.Select(s => s.ChildCount), Has.All.EqualTo(1));
            Assert.That(counter.Count, Is.EqualTo(1));
        });
    }

    [TestCase(1)]
    [TestCase(6)]
    public async Task AllSummaries_AreOneQuery_HoweverManyItems(int children)
    {
        var parent = await CreateTreeAsync(children);
        var counter = new CommandCounter();
        await using var db = CountingContext(counter);

        var summaries = await WorkItems(db).GetAllSummariesAsync(0, WorkItemService.MaxPageSize, parent.SpaceId);

        Assert.Multiple(() =>
        {
            Assert.That(summaries, Has.Count.EqualTo(1 + 2 * children));
            Assert.That(counter.Count, Is.EqualTo(1));
        });
    }

    // Searching finds the page's Numbers first, then reads that page: two queries, not one per item.
    [TestCase(1)]
    [TestCase(6)]
    public async Task SearchSummaries_AreTwoQueries_HoweverManyItems(int children)
    {
        var parent = await CreateTreeAsync(children);
        var counter = new CommandCounter();
        await using var db = CountingContext(counter);
        var query = new WorkItemQueryService(db, new PostgresWorkItemHierarchy(db), new PostgresWorkItemSearch());

        var page = await query.QuerySummariesAsync(new WorkItemQuery(SpaceIds: [parent.SpaceId]));

        Assert.Multiple(() =>
        {
            Assert.That(page.Items, Has.Count.EqualTo(1 + 2 * children));
            Assert.That(counter.Count, Is.EqualTo(2));
        });
    }

    /// <summary>
    /// A parent in a Space of its own, with <paramref name="children"/> assigned children that
    /// each have one child, so every name and the child count are read.
    /// </summary>
    private async Task<WorkItemDto> CreateTreeAsync(int children)
    {
        var space = await ApiClient.CreateSpaceAsync("Query count");
        var parent = await _client.CreateWorkItemAsync("Parent", spaceId: space);
        for (var i = 0; i < children; i++)
        {
            var child = await _client.CreateWorkItemAsync($"Child {i}", parent.Id);
            await _client.CreateWorkItemAsync($"Grandchild {i}", child.Id);
            (await _client.PutJsonAsync($"/api/work-items/{child.Id}/assignee", new AssignWorkItemRequest(_userId)))
                .EnsureSuccessStatusCode();
        }
        return parent;
    }

    private static WeaverDbContext CountingContext(CommandCounter counter) =>
        new(new DbContextOptionsBuilder<WeaverDbContext>()
            .UseNpgsql(IntegrationTestEnvironment.Factory.ConnectionString)
            .AddInterceptors(counter)
            .Options);

    private static WorkItemService WorkItems(WeaverDbContext db) =>
        new(db, new PostgresWorkItemHierarchy(db), new NoCaller());
}
