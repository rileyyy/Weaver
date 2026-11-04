using System.Net;
using Weaver.Api.Contracts;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// The Trash against Postgres (ADR-0007): the query filter, include-deleted, restore's placement
/// and the constraints a trashed row still takes part in.
/// </summary>
[TestFixture]
public class TrashTests
{
    private HttpClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        (_client, _) = await ApiClient.SignedInAsync();
    }

    private static string Unique(string title) => $"{title} {Guid.NewGuid():N}";

    private Task<HttpResponseMessage> RestoreAsync(Guid id) => _client.PostAsync($"/api/trash/{id}/restore", null);

    [Test]
    public async Task ADeletedItem_IsListedInTheTrash_AndOnlyIncludeDeletedFindsIt()
    {
        var space = await ApiClient.CreateSpaceAsync("Trash list");
        var title = Unique("Old shed");
        var item = await _client.CreateWorkItemAsync(title, spaceId: space);

        await _client.DeleteAsync($"/api/work-items/{item.Id}");
        var trash = await (await _client.GetAsync($"/api/trash?spaceId={space}")).ReadAsync<List<TrashedWorkItemDto>>();
        var search = await (await _client.GetAsync($"/api/work-items/search?spaceIds={space}")).ReadAsync<WorkItemPageDto>();
        var withDeleted = await (await _client.GetAsync($"/api/work-items/search?spaceIds={space}&includeDeleted=true"))
            .ReadAsync<WorkItemPageDto>();

        Assert.Multiple(() =>
        {
            Assert.That(trash.Single().Item.Id, Is.EqualTo(item.Id));
            Assert.That(trash.Single().Item.Number, Is.EqualTo(item.Number));
            Assert.That(trash.Single().DeletedByName, Is.Not.Null);
            Assert.That(search.Items, Is.Empty);
            Assert.That(withDeleted.Items.Single().TrashedAtUtc, Is.Not.Null);
        });
    }

    [Test]
    public async Task Restore_WhenTheParentIsStillTrashed_PutsItUnderTheGrandparent()
    {
        var house = await _client.CreateWorkItemAsync(Unique("House"));
        var kitchen = await _client.CreateWorkItemAsync("Kitchen", house.Id);
        var tiles = await _client.CreateWorkItemAsync("Tiles", kitchen.Id);
        await _client.DeleteAsync($"/api/work-items/{tiles.Id}");
        await _client.DeleteAsync($"/api/work-items/{kitchen.Id}");

        var response = await RestoreAsync(tiles.Id);

        Assert.Multiple(async () =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That((await response.ReadAsync<WorkItemDto>()).ParentId, Is.EqualTo(house.Id));
        });
    }

    [Test]
    public async Task Restore_AnItemDeletedWithItsParent_Returns400()
    {
        var kitchen = await _client.CreateWorkItemAsync(Unique("Kitchen"));
        var tiles = await _client.CreateWorkItemAsync("Tiles", kitchen.Id);
        await _client.DeleteAsync($"/api/work-items/{kitchen.Id}?cascade=true");

        var response = await RestoreAsync(tiles.Id);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task AReadOnlyKey_CanListTheTrash_ButNotRestore()
    {
        var item = await _client.CreateWorkItemAsync(Unique("Old shed"));
        await _client.DeleteAsync($"/api/work-items/{item.Id}");
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);

        var list = await agent.GetAsync("/api/trash");
        var restore = await agent.PostAsync($"/api/trash/{item.Id}/restore", null);

        Assert.Multiple(() =>
        {
            Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(restore.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });
    }

    [Test]
    public async Task AReadWriteKey_CanRestore()
    {
        var item = await _client.CreateWorkItemAsync(Unique("Old shed"));
        await _client.DeleteAsync($"/api/work-items/{item.Id}");
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);

        var restore = await agent.PostAsync($"/api/trash/{item.Id}/restore", null);

        Assert.That(restore.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    // A trashed row still holds its status's foreign key, so the status delete has to move it.
    [Test]
    public async Task DeletingAStatusUsedOnlyInTheTrash_MovesTheTrashedItem_InsteadOfFailingOnTheForeignKey()
    {
        var admin = await ApiClient.FirstAdminAsync();
        var space = await ApiClient.CreateSpaceAsync("Trash status");
        var temp = await (await admin.PostJsonAsync(
            "/api/statuses",
            new CreateStatusRequest(space, "Temp", StatusCategory.Doing, "#00897B"))).ReadAsync<StatusDto>();
        var item = await (await _client.PostJsonAsync(
            "/api/work-items",
            new CreateWorkItemRequest(Unique("Old shed"), null, null, temp.Id, null, SpaceId: space))).ReadAsync<WorkItemDto>();
        await _client.DeleteAsync($"/api/work-items/{item.Id}");
        var done = await ApiClient.StatusIdAsync(space, "Done");

        var delete = await admin.DeleteAsync($"/api/statuses/{temp.Id}?replacementId={done}");
        var restored = await (await RestoreAsync(item.Id)).ReadAsync<WorkItemDto>();

        Assert.Multiple(() =>
        {
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(restored.StatusId, Is.EqualTo(done));
        });
    }

    // The (source, date) pair is unique, so regenerating over a trashed copy would fail the save.
    [Test]
    public async Task ChangingASchedule_AfterAnOccurrenceWasTrashed_DoesNotRecreateIt()
    {
        var template = await _client.CreateWorkItemAsync(Unique("Bins"));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var everyDay = Enum.GetValues<DayOfWeek>();
        await _client.PutJsonAsync(
            $"/api/work-items/{template.Id}/recurrence",
            new SetWorkItemRecurrenceRequest(RecurrenceFrequency.Weekly, everyDay, today, null));
        var copies = await OccurrencesAsync(template);
        await _client.DeleteAsync($"/api/work-items/{copies[0].Id}");

        var reset = await _client.PutJsonAsync(
            $"/api/work-items/{template.Id}/recurrence",
            new SetWorkItemRecurrenceRequest(RecurrenceFrequency.Weekly, everyDay, today.AddDays(-1), null));

        Assert.Multiple(async () =>
        {
            Assert.That(reset.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That((await OccurrencesAsync(template)).Select(c => c.Id), Does.Not.Contain(copies[0].Id));
            Assert.That(await OccurrencesAsync(template), Has.Count.EqualTo(copies.Count - 1));
        });
    }

    private async Task<List<WorkItemDto>> OccurrencesAsync(WorkItemDto template)
    {
        var siblings = await (await _client.GetAsync($"/api/work-items/search?spaceIds={template.SpaceId}&text={Uri.EscapeDataString(template.Title)}&limit=1000"))
            .ReadAsync<WorkItemPageDto>();
        return siblings.Items.Where(w => w.RecurrenceSourceId == template.Id).ToList();
    }
}
