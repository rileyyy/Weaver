using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;

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
        var item = await _client.CreateWorkItemAsync("Enum check");

        var json = await _client.GetStringAsync($"/api/work-items/{item.Id}");
        var priority = JsonDocument.Parse(json).RootElement.GetProperty("priority").GetString();

        Assert.That(priority, Is.EqualTo("Medium"));
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

        await _client.PostJsonAsync(
            $"/api/work-items/{item.Id}/tags",
            new SetTagsWorkItemRequest(["urgent", "needs review"]));
        var reloaded = await (await _client.GetAsync($"/api/work-items/{item.Id}")).ReadAsync<WorkItemDto>();

        Assert.That(reloaded.Tags, Is.EqualTo(new[] { "urgent", "needs review" }));
    }

    [Test]
    public async Task Delete_ASubtreeWithLinks_RemovesTheLinksToo()
    {
        var parent = await _client.CreateWorkItemAsync("Parent");
        var child = await _client.CreateWorkItemAsync("Child", parent.Id);
        var outsider = await _client.CreateWorkItemAsync("Outsider");
        await _client.PostJsonAsync(
            $"/api/work-items/{child.Id}/links",
            new CreateWorkItemLinkRequest(outsider.Id));

        var delete = await _client.DeleteAsync($"/api/work-items/{parent.Id}?cascade=true");
        var outsiderLinks = await (await _client.GetAsync($"/api/work-items/{outsider.Id}/links"))
            .ReadAsync<List<WorkItemLinkDto>>();

        Assert.Multiple(() =>
        {
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(outsiderLinks, Is.Empty);
        });
    }

    [Test]
    public async Task Delete_ABoardsScopeItem_Returns409()
    {
        var scope = await _client.CreateWorkItemAsync("Board scope");
        await _client.PostJsonAsync("/api/boards", new CreateBoardRequest("Scoped board", scope.Id));

        var response = await _client.DeleteAsync($"/api/work-items/{scope.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task UpdateDetails_WithAStaleVersion_Returns409()
    {
        var item = await _client.CreateWorkItemAsync("Original");
        var firstEdit = await (await _client.PutJsonAsync(
            $"/api/work-items/{item.Id}/details",
            new UpdateWorkItemDetailsRequest("First edit", null, null, Domain.WorkItemPriority.High, item.Version)))
            .ReadAsync<WorkItemDto>();

        var staleEdit = await _client.PutJsonAsync(
            $"/api/work-items/{item.Id}/details",
            new UpdateWorkItemDetailsRequest("Stale edit", null, null, Domain.WorkItemPriority.Low, item.Version));
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

        var response = await _client.PostJsonAsync(
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
