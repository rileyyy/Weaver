using System.Net;
using System.Net.Http.Json;
using Weaver.Api.Contracts;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// The admin operations whose correctness depends on Postgres: the unique order index during
/// a reorder, moving items between statuses, and deleting users with real foreign keys. Each
/// test cleans up the lookups it adds, since every test shares the database.
/// </summary>
[TestFixture]
public class AdminTests
{
    [Test]
    public async Task LookupChanges_ByANonAdmin_Return403()
    {
        var (client, _) = await ApiClient.SignedInAsync();

        var response = await client.PostJsonAsync("/api/work-item-priorities", new LookupNameRequest("Nope"));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        });
    }

    [Test]
    public async Task Me_ReportsWhetherTheUserIsAnAdmin()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();

        var me = await (await admin.GetAsync("/api/auth/me")).ReadAsync<UserDto>();

        Assert.That(me.IsAdmin, Is.True);
    }

    [Test]
    public async Task ReorderingPriorities_CanSwapTwoNeighbours()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var first = await CreatePriorityAsync(admin);
        var second = await CreatePriorityAsync(admin);
        try
        {
            var current = await (await admin.GetAsync("/api/work-item-priorities")).ReadAsync<List<WorkItemPriorityDto>>();
            var swapped = current.Select(p => p.Id).ToList();
            (swapped[^1], swapped[^2]) = (swapped[^2], swapped[^1]);

            var reordered = await (await admin.PutJsonAsync("/api/work-item-priorities/order", new ReorderLookupRequest(swapped)))
                .ReadAsync<List<WorkItemPriorityDto>>();
            var stored = await (await admin.GetAsync("/api/work-item-priorities")).ReadAsync<List<WorkItemPriorityDto>>();

            Assert.Multiple(() =>
            {
                Assert.That(reordered.Select(p => p.Id), Is.EqualTo(swapped));
                Assert.That(stored.Select(p => p.Id), Is.EqualTo(swapped));
                Assert.That(stored[^1].Id, Is.EqualTo(first.Id));
                Assert.That(stored[^2].Id, Is.EqualTo(second.Id));
            });
        }
        finally
        {
            await admin.DeleteAsync($"/api/work-item-priorities/{first.Id}");
            await admin.DeleteAsync($"/api/work-item-priorities/{second.Id}");
        }
    }

    [Test]
    public async Task DeletingAStatusInUse_NeedsAReplacement_AndMovesItsItemsToIt()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var status = await (await admin.PostJsonAsync(
                "/api/statuses",
                new StatusDetailsRequest($"Temp {Guid.NewGuid():N}"[..20], StatusCategory.Doing, "#00897B")))
            .ReadAsync<StatusDto>();
        var lane = await admin.CreateWorkItemAsync("Lane");
        var item = await admin.CreateWorkItemAsync("Card", lane.Id);
        await (await admin.PutJsonAsync(
            $"/api/work-items/{item.Id}/status",
            new ChangeWorkItemStatusRequest(status.Id, null))).ReadAsync<WorkItemDto>();

        var withoutReplacement = await admin.DeleteAsync($"/api/statuses/{status.Id}");
        var withReplacement = await admin.DeleteAsync($"/api/statuses/{status.Id}?replacementId={SeededStatuses.ToDo}");
        var moved = await (await admin.GetAsync($"/api/work-items/{item.Id}")).ReadAsync<WorkItemDto>();

        Assert.Multiple(() =>
        {
            Assert.That(withoutReplacement.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(withReplacement.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(moved.StatusId, Is.EqualTo(SeededStatuses.ToDo));
            Assert.That(moved.ParentId, Is.EqualTo(lane.Id));
        });
    }

    [Test]
    public async Task DeactivatedUser_CanNoLongerSignInOrRefresh()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var (_, member) = await ApiClient.SignedInAsync();

        await (await admin.PutJsonAsync($"/api/admin/users/{member.User.Id}/active", new SetUserActiveRequest(false)))
            .ReadAsync<AdminUserDto>();
        var login = await ApiClient.Anonymous().PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(member.User.Username, ApiClient.Password));
        var refresh = await ApiClient.Anonymous().PostJsonAsync(
            "/api/auth/refresh",
            new RefreshRequest(member.RefreshToken));

        Assert.Multiple(() =>
        {
            Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(refresh.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        });
    }

    [Test]
    public async Task DeletingAUser_UnassignsTheirWork_ButNotIfTheyWroteComments()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var (_, member) = await ApiClient.SignedInAsync();
        var (commenterClient, commenter) = await ApiClient.SignedInAsync();
        var item = await admin.CreateWorkItemAsync("Assigned");
        await (await admin.PutJsonAsync($"/api/work-items/{item.Id}/assignee", new AssignWorkItemRequest(member.User.Id)))
            .ReadAsync<WorkItemDto>();
        await (await commenterClient.PostJsonAsync($"/api/work-items/{item.Id}/comments", new CreateCommentRequest("Hi")))
            .ReadAsync<CommentDto>();

        var deleted = await admin.DeleteAsync($"/api/admin/users/{member.User.Id}");
        var refused = await admin.DeleteAsync($"/api/admin/users/{commenter.User.Id}");
        var unassigned = await (await admin.GetAsync($"/api/work-items/{item.Id}")).ReadAsync<WorkItemDto>();

        Assert.Multiple(() =>
        {
            Assert.That(deleted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(refused.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(unassigned.AssignedToUserId, Is.Null);
        });
    }

    private static async Task<WorkItemPriorityDto> CreatePriorityAsync(HttpClient admin) =>
        await (await admin.PostJsonAsync(
                "/api/work-item-priorities",
                new LookupNameRequest($"P {Guid.NewGuid():N}"[..20])))
            .ReadAsync<WorkItemPriorityDto>();
}
