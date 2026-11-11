using System.Net;
using System.Text.Json.Nodes;
using Weaver.Runner.Tests.Fakes;
using Weaver.Runner.WeaverApi;

namespace Weaver.Runner.Tests.WeaverApi;

[TestFixture]
public class HttpMentionsWeaverClientTests
{
    private const string Key = "wvr_test";
    private const string RunId = "run-1";
    private StubHttpHandler _handler = null!;
    private HttpMentionsWeaverClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new StubHttpHandler();
        _client = new HttpMentionsWeaverClient(
            new HttpClient(_handler) { BaseAddress = new Uri("http://weaver.test/") }, Key, RunId);
    }

    [TearDown]
    public void TearDown() => _handler.Dispose();

    [Test]
    public async Task PendingMentions_AreReadWithTheirCommentAndItem()
    {
        var commentId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        _handler.Respond = _ => StubHttpHandler.Json(
            $$"""
            [{"comment":{"id":"{{commentId}}","workItemId":"{{itemId}}","authorUserId":"{{Guid.Empty}}","authorUsername":"riley",
              "body":"@claude help","createdAtUtc":"2026-11-11T12:00:00+00:00","updatedAtUtc":null},
              "workItemId":"{{itemId}}","workItemNumber":42,"workItemTitle":"Kitchen quotes","mentionedAtUtc":"2026-11-11T12:00:00+00:00"}]
            """);

        var pending = (await _client.ListPendingMentionsAsync(CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(_handler.Requests.Single().Request.RequestUri!.AbsolutePath, Is.EqualTo("/api/mentions/pending"));
            Assert.That(pending.Comment.Id, Is.EqualTo(commentId));
            Assert.That(pending.Comment.AuthorUsername, Is.EqualTo("riley"));
            Assert.That(pending.Comment.Body, Is.EqualTo("@claude help"));
            Assert.That(pending.WorkItemId, Is.EqualTo(itemId));
            Assert.That(pending.WorkItemNumber, Is.EqualTo(42));
        });
    }

    [TestCase(HttpStatusCode.NotFound)]
    [TestCase(HttpStatusCode.BadRequest)]
    public void AFailedPendingList_MakesWeaverUnavailable(HttpStatusCode status)
    {
        _handler.Respond = _ => StubHttpHandler.Json("{}", status);

        Assert.ThrowsAsync<WeaverUnavailableException>(() => _client.ListPendingMentionsAsync(CancellationToken.None));
    }

    [Test]
    public async Task TheFirstToDoStatus_InBoardOrder_IsWhereSubItemsStart()
    {
        var spaceId = Guid.NewGuid();
        var later = Guid.NewGuid();
        var first = Guid.NewGuid();
        _handler.Respond = _ => StubHttpHandler.Json(
            $$"""
            [{"id":"{{Guid.NewGuid()}}","order":0,"category":"Doing"},
             {"id":"{{later}}","order":2,"category":"ToDo"},
             {"id":"{{first}}","order":1,"category":"ToDo"}]
            """);

        var statusId = await _client.GetFirstToDoStatusIdAsync(spaceId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(statusId, Is.EqualTo(first));
            Assert.That(_handler.Requests.Single().Request.RequestUri!.PathAndQuery, Is.EqualTo($"/api/statuses?spaceId={spaceId}"));
        });
    }

    [Test]
    public async Task AddSubItem_PostsAChildItem_AndReturnsItsNumber()
    {
        var parent = Guid.NewGuid();
        var status = Guid.NewGuid();
        _handler.Respond = _ => StubHttpHandler.Json($$"""{"id":"{{Guid.NewGuid()}}","number":57}""", HttpStatusCode.Created);

        var number = await _client.AddSubItemAsync(parent, "Call fitters", status, "Runner mentions", CancellationToken.None);

        var (request, body) = _handler.Requests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(number, Is.EqualTo(57));
            Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo("/api/work-items"));
            Assert.That(SameJson(body!, $$"""{"title":"Call fitters","parentId":"{{parent}}","statusId":"{{status}}"}"""), Is.True, body);
        });
    }

    [Test]
    public async Task Writes_CarryTheActorLabelAndRunId()
    {
        var item = Guid.NewGuid();
        _handler.Respond = _ => StubHttpHandler.Json("{}", HttpStatusCode.Created);

        await _client.AddCommentAsync(item, "Here you go.", "Runner mentions", CancellationToken.None);
        await _client.AddUrlLinkAsync(item, "https://a.example/", "Quote", "Runner mentions", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_handler.Requests.Select(r => r.Request.RequestUri!.AbsolutePath), Is.EqualTo(new[]
            {
                $"/api/work-items/{item}/comments",
                $"/api/work-items/{item}/url-links",
            }));
            Assert.That(SameJson(_handler.Requests[0].Body!, """{"body":"Here you go."}"""), Is.True);
            Assert.That(SameJson(_handler.Requests[1].Body!, """{"url":"https://a.example/","label":"Quote"}"""), Is.True);
            Assert.That(_handler.Requests.Select(r => r.Request.Headers.GetValues("X-Agent-Run").Single()), Is.All.EqualTo(RunId));
            Assert.That(_handler.Requests.Select(r => r.Request.Headers.GetValues("X-Actor-Label").Single()), Is.All.EqualTo("Runner mentions"));
        });
    }

    [Test]
    public void ARefusedReply_IsRejectedForThatMention()
    {
        _handler.Respond = _ => StubHttpHandler.Json("""{"title":"Not found"}""", HttpStatusCode.NotFound);

        Assert.ThrowsAsync<WeaverRejectedException>(() =>
            _client.AddCommentAsync(Guid.NewGuid(), "Here you go.", "Runner mentions", CancellationToken.None));
    }

    private static bool SameJson(string actual, string expected) =>
        JsonNode.DeepEquals(JsonNode.Parse(actual), JsonNode.Parse(expected));
}
