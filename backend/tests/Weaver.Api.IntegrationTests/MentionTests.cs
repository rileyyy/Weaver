using System.Net;
using Weaver.Api.Contracts;
using Weaver.Domain;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// Asking an agent through an @ mention (ticket 51), end to end against Postgres: the handle's
/// unique index, the mention rows saved with the comment, and the pending query.
/// </summary>
[TestFixture]
public class MentionTests
{
    [Test]
    public async Task AMentionedAgent_SeesTheRequestPending_UntilItReplies()
    {
        var (agent, account) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadWrite);
        var handle = await GiveHandleAsync(account.Id);
        var (human, _) = await ApiClient.SignedInAsync();
        var item = await human.CreateWorkItemAsync("Kitchen quotes");

        await human.PostJsonAsync($"/api/work-items/{item.Id}/comments", new CreateCommentRequest($"@{handle} find three quotes"));
        var before = await (await agent.GetAsync("/api/mentions/pending")).ReadAsync<List<PendingMentionDto>>();
        await agent.PostJsonAsync($"/api/work-items/{item.Id}/comments", new CreateCommentRequest("Here are three quotes."));
        var after = await (await agent.GetAsync("/api/mentions/pending")).ReadAsync<List<PendingMentionDto>>();

        Assert.Multiple(() =>
        {
            Assert.That(before, Has.Count.EqualTo(1));
            Assert.That(before[0].WorkItemId, Is.EqualTo(item.Id));
            Assert.That(before[0].WorkItemTitle, Is.EqualTo("Kitchen quotes"));
            Assert.That(before[0].Comment.Body, Is.EqualTo($"@{handle} find three quotes"));
            Assert.That(after, Is.Empty);
        });
    }

    [Test]
    public async Task AHandle_CanBelongToOnlyOneAgent()
    {
        var admin = await ApiClient.FirstAdminAsync();
        var (_, first) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);
        var (_, second) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);
        var handle = await GiveHandleAsync(first.Id);

        var response = await admin.PutJsonAsync($"/api/admin/agents/{second.Id}/handle", new SetHandleRequest(handle));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task AnInvalidHandle_IsABadRequest()
    {
        var admin = await ApiClient.FirstAdminAsync();
        var (_, agent) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);

        var response = await admin.PutJsonAsync($"/api/admin/agents/{agent.Id}/handle", new SetHandleRequest("Not A Handle"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ActiveAgentsHandles_AreListedForTheCommentBox()
    {
        var (_, agent) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);
        var handle = await GiveHandleAsync(agent.Id);
        var (human, _) = await ApiClient.SignedInAsync();

        var users = await (await human.GetAsync("/api/users")).ReadAsync<List<UserDto>>();

        Assert.That(users.Single(u => u.Id == agent.Id).Handle, Is.EqualTo(handle));
    }

    private static async Task<string> GiveHandleAsync(Guid agentId)
    {
        var admin = await ApiClient.FirstAdminAsync();
        var handle = $"agent-{Guid.NewGuid():N}"[..30];
        var response = await admin.PutJsonAsync($"/api/admin/agents/{agentId}/handle", new SetHandleRequest(handle));
        var updated = await response.ReadAsync<AgentAccountDto>();
        Assert.That(updated.Handle, Is.EqualTo(handle));
        return handle;
    }
}
