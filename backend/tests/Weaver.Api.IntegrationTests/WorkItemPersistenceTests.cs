using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

[TestFixture]
public class WorkItemPersistenceTests
{
    private HttpClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        (_client, _) = await ApiClient.SignedInAsync();
    }

    [Test]
    public async Task Enums_AreSentAsTheirNames()
    {
        var json = await _client.GetStringAsync($"/api/statuses?spaceId={await ApiClient.SharedSpaceIdAsync()}");
        var category = JsonDocument.Parse(json).RootElement[0].GetProperty("category").GetString();

        Assert.That(category, Is.EqualTo("ToDo"));
    }

    [Test]
    public async Task Priorities_AreSeededInOrder_AndCanBeSetOnAWorkItem()
    {
        var space = await ApiClient.CreateSpaceAsync("Priorities");
        var priorities = await (await _client.GetAsync($"/api/work-item-priorities?spaceId={space}"))
            .ReadAsync<List<WorkItemPriorityDto>>();
        var item = await _client.CreateWorkItemAsync("Prioritised", spaceId: space);
        var high = await ApiClient.PriorityIdAsync(space, "High");

        var updated = (await (await _client.PatchJsonAsync(
            $"/api/work-items/{item.Id}/details",
            new { priorityId = high }))
            .ReadAsync<WorkItemWriteResponse>()).Item;

        Assert.Multiple(() =>
        {
            Assert.That(priorities.Select(p => p.Name), Is.EqualTo(new[] { "Low", "Medium", "High", "Urgent" }));
            Assert.That(item.PriorityId, Is.Null);
            Assert.That(updated.PriorityId, Is.EqualTo(high));
        });
    }

    [Test]
    public async Task Numbers_AreAssignedByTheDatabaseInCreationOrder()
    {
        var first = await _client.CreateWorkItemAsync("First");
        var second = await _client.CreateWorkItemAsync("Second");

        Assert.That(second.Number, Is.GreaterThan(first.Number));
    }

    [Test]
    public async Task Tags_RoundTripThroughTheTextArrayColumn()
    {
        var item = await _client.CreateWorkItemAsync("Tagged");

        await _client.PatchJsonAsync(
            $"/api/work-items/{item.Id}/tags",
            new { tags = new[] { "urgent", "needs review" } });
        var reloaded = await (await _client.GetAsync($"/api/work-items/{item.Id}")).ReadAsync<WorkItemDto>();

        Assert.That(reloaded.Tags, Is.EqualTo(new[] { "urgent", "needs review" }));
    }

    [Test]
    public async Task Delete_ASubtreeWithLinks_HidesTheLinksUntilItsRestored()
    {
        var parent = await _client.CreateWorkItemAsync("Parent");
        var child = await _client.CreateWorkItemAsync("Child", parent.Id);
        var outsider = await _client.CreateWorkItemAsync("Outsider");
        await _client.PostJsonAsync(
            $"/api/work-items/{child.Id}/links",
            new CreateWorkItemLinkRequest(outsider.Id, WorkItemLinkRelation.Related));

        var delete = await _client.DeleteAsync($"/api/work-items/{parent.Id}?cascade=true");
        var whileTrashed = await (await _client.GetAsync($"/api/work-items/{outsider.Id}/links"))
            .ReadAsync<List<WorkItemLinkDto>>();
        await _client.PostAsync($"/api/trash/{parent.Id}/restore", null);
        var afterRestore = await (await _client.GetAsync($"/api/work-items/{outsider.Id}/links"))
            .ReadAsync<List<WorkItemLinkDto>>();

        Assert.Multiple(() =>
        {
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(whileTrashed, Is.Empty);
            Assert.That(afterRestore, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task UpdateDetails_WithAStaleVersion_Returns409()
    {
        var item = await _client.CreateWorkItemAsync("Original");
        var high = await ApiClient.PriorityIdAsync(item.SpaceId, "High");
        var low = await ApiClient.PriorityIdAsync(item.SpaceId, "Low");
        var firstEdit = (await (await _client.PatchJsonAsync(
            $"/api/work-items/{item.Id}/details",
            new { title = "First edit", priorityId = high, expectedVersion = item.Version }))
            .ReadAsync<WorkItemWriteResponse>()).Item;

        var staleEdit = await _client.PatchJsonAsync(
            $"/api/work-items/{item.Id}/details",
            new { title = "Stale edit", priorityId = low, expectedVersion = item.Version });
        var current = await (await _client.GetAsync($"/api/work-items/{item.Id}")).ReadAsync<WorkItemDto>();

        Assert.Multiple(() =>
        {
            Assert.That(firstEdit.Version, Is.Not.EqualTo(item.Version), "xmin must change on save");
            Assert.That(staleEdit.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(current.Title, Is.EqualTo("First edit"));
        });
    }

    [Test]
    public async Task UnknownWorkItem_Returns404ProblemDetails()
    {
        var id = Guid.NewGuid();

        var response = await _client.PutJsonAsync(
            $"/api/work-items/{id}/assignee",
            new AssignWorkItemRequest(null));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(problem?.Detail, Does.Contain(id.ToString()));
        });
    }
}
