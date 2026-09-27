using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Weaver.Api.Contracts;
using Weaver.Infrastructure;

namespace Weaver.Api.IntegrationTests;

/// <summary>The recursive-CTE tree walks and rank queries, which only run on Postgres.</summary>
[TestFixture]
public class HierarchyQueryTests
{
    private HttpClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        (_client, _) = await ApiClient.SignedInAsync();
    }

    [Test]
    public async Task CascadeDelete_RemovesADeepSubtree()
    {
        var chain = await CreateChainAsync(depth: 5);

        var response = await _client.DeleteAsync($"/api/work-items/{chain[0].Id}?cascade=true");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        foreach (var item in chain)
        {
            var get = await _client.GetAsync($"/api/work-items/{item.Id}");
            Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), item.Title);
        }
    }

    [Test]
    public async Task Reparent_UnderADeepDescendant_Returns409()
    {
        var chain = await CreateChainAsync(depth: 4);

        var response = await _client.PutJsonAsync(
            $"/api/work-items/{chain[0].Id}/parent",
            new ReparentWorkItemRequest(chain[^1].Id, null));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Reparent_UnderAnUnrelatedItem_Succeeds()
    {
        var chain = await CreateChainAsync(depth: 3);
        var outsider = await _client.CreateWorkItemAsync("Outsider");

        var moved = await (await _client.PutJsonAsync(
            $"/api/work-items/{outsider.Id}/parent",
            new ReparentWorkItemRequest(chain[^1].Id, null))).ReadAsync<WorkItemDto>();

        Assert.That(moved.ParentId, Is.EqualTo(chain[^1].Id));
    }

    [Test]
    public async Task Reparent_IntoATreeThatAlreadyHasACycle_FailsLoudly()
    {
        var a = await _client.CreateWorkItemAsync("A");
        var b = await _client.CreateWorkItemAsync("B", a.Id);
        var outsider = await _client.CreateWorkItemAsync("Outsider");
        await ExecuteSqlAsync($"UPDATE \"WorkItems\" SET \"ParentId\" = {b.Id} WHERE \"Id\" = {a.Id}");

        var response = await _client.PutJsonAsync(
            $"/api/work-items/{outsider.Id}/parent",
            new ReparentWorkItemRequest(a.Id, null));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        await ExecuteSqlAsync($"UPDATE \"WorkItems\" SET \"ParentId\" = NULL WHERE \"Id\" = {a.Id}");
    }

    [Test]
    public async Task All_PagesWithoutOverlap_AndRejectsAnInvalidLimit()
    {
        await _client.CreateWorkItemAsync("Paged 1");
        await _client.CreateWorkItemAsync("Paged 2");

        var page1 = await (await _client.GetAsync("/api/work-items/all?offset=0&limit=1")).ReadAsync<List<WorkItemDto>>();
        var page2 = await (await _client.GetAsync("/api/work-items/all?offset=1&limit=1")).ReadAsync<List<WorkItemDto>>();
        var invalid = await _client.GetAsync("/api/work-items/all?limit=0");

        Assert.Multiple(() =>
        {
            Assert.That(page1, Has.Count.EqualTo(1));
            Assert.That(page2.Single().Id, Is.Not.EqualTo(page1.Single().Id));
            Assert.That(invalid.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task Create_AfterTiedSiblings_RespacesTheCell()
    {
        var parent = await _client.CreateWorkItemAsync("Tie parent");
        var first = await _client.CreateWorkItemAsync("First", parent.Id);
        var second = await CreateAfterAsync("Second", parent.Id, first.Id);
        await ExecuteSqlAsync($"UPDATE \"WorkItems\" SET \"Rank\" = 1 WHERE \"Id\" IN ({first.Id}, {second.Id})");

        var inserted = await CreateAfterAsync("Inserted", parent.Id, first.Id);
        var cell = await (await _client.GetAsync($"/api/work-items?parentId={parent.Id}")).ReadAsync<List<WorkItemDto>>();

        Assert.Multiple(() =>
        {
            Assert.That(cell.Select(w => w.Id), Is.EqualTo(new[] { first.Id, inserted.Id, second.Id }));
            Assert.That(cell.Select(w => w.Rank), Is.Unique);
        });
    }

    private async Task<List<WorkItemDto>> CreateChainAsync(int depth)
    {
        var chain = new List<WorkItemDto>();
        Guid? parentId = null;
        for (var i = 0; i < depth; i++)
        {
            var item = await _client.CreateWorkItemAsync($"Level {i}", parentId);
            chain.Add(item);
            parentId = item.Id;
        }

        return chain;
    }

    private async Task<WorkItemDto> CreateAfterAsync(string title, Guid parentId, Guid afterId) =>
        await (await _client.PostJsonAsync(
            "/api/work-items",
            new CreateWorkItemRequest(title, null, parentId, SeededStatuses.ToDo, afterId))).ReadAsync<WorkItemDto>();

    private static async Task ExecuteSqlAsync(FormattableString sql)
    {
        using var scope = IntegrationTestEnvironment.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<WeaverDbContext>().Database.ExecuteSqlAsync(sql);
    }
}
