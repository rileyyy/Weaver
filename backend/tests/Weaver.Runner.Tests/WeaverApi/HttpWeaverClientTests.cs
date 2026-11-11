using System.Net;
using System.Text.Json.Nodes;
using Weaver.Runner.Tests.Fakes;
using Weaver.Runner.WeaverApi;

namespace Weaver.Runner.Tests.WeaverApi;

[TestFixture]
public class HttpWeaverClientTests
{
    private const string Key = "wvr_test";
    private StubHttpHandler _handler = null!;
    private HttpWeaverClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new StubHttpHandler();
        _client = new HttpWeaverClient(
            new HttpClient(_handler) { BaseAddress = new Uri("http://weaver.test/") }, Key);
    }

    [TearDown]
    public void TearDown() => _handler.Dispose();

    [Test]
    public async Task UnresolvedItems_LeaveOutDone_AndFollowEveryPage()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        _handler.Respond = request => request.RequestUri!.Query.Contains("cursor=")
            ? StubHttpHandler.Json($$"""{"items":[{{Item(second, 2)}}],"nextCursor":null}""")
            : StubHttpHandler.Json($$"""{"items":[{{Item(first, 1)}}],"nextCursor":"c1"}""");

        var items = await _client.ListUnresolvedWhenItemsAsync(CancellationToken.None);

        var queries = _handler.Requests.Select(r => r.Request.RequestUri!.PathAndQuery).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(items.Select(i => i.Id), Is.EqualTo(new[] { first, second }));
            Assert.That(queries[0], Does.StartWith("/api/work-items/search?")
                .And.Contain("unresolvedWhenOnly=true")
                .And.Contain("statusCategories=ToDo&statusCategories=Doing")
                .And.Not.Contain("Done"));
            Assert.That(queries[1], Does.Contain("cursor=c1"));
        });
    }

    [Test]
    public async Task Items_ReadTheFieldsTheRunnerNeeds()
    {
        var id = Guid.NewGuid();
        _handler.Respond = _ => StubHttpHandler.Json($$"""{"items":[{{Item(id, 42)}}],"nextCursor":null}""");

        var item = (await _client.ListUnresolvedWhenItemsAsync(CancellationToken.None)).Single();

        Assert.That(item, Is.EqualTo(new WeaverWorkItem(
            id, 42, "Paint", "after drywall", SpaceId, "Home", 40, "Kitchen", null, new DateOnly(2026, 12, 6), 9)));
    }

    [Test]
    public async Task Search_EscapesTheText_AndLeavesOutDone()
    {
        _handler.Respond = _ => StubHttpHandler.Json("""{"items":[],"nextCursor":null}""");

        await _client.SearchOpenItemsAsync("café & co", 10, CancellationToken.None);

        Assert.That(_handler.Requests.Single().Request.RequestUri!.Query,
            Does.Contain("text=caf%C3%A9%20%26%20co").And.Contain("statusCategories=ToDo&statusCategories=Doing").And.Contain("limit=10"));
    }

    [Test]
    public async Task TodayQuickPicksAndLinks_AreRead()
    {
        var linked = Guid.NewGuid();
        _handler.Respond = request => request.RequestUri!.AbsolutePath switch
        {
            "/api/calendar/today" => StubHttpHandler.Json("""{"today":"2026-11-11"}"""),
            "/api/quick-picks" => StubHttpHandler.Json("""[{"pick":"NextWeek","startDate":"2026-11-16","endDate":"2026-11-22"}]"""),
            _ => StubHttpHandler.Json($$"""[{"id":"{{Guid.NewGuid()}}","type":"Related","linkedWorkItemId":"{{linked}}"}]"""),
        };

        var today = await _client.GetTodayAsync(CancellationToken.None);
        var picks = await _client.ListQuickPicksAsync(CancellationToken.None);
        var links = await _client.ListLinkedItemIdsAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(today, Is.EqualTo(new DateOnly(2026, 11, 11)));
            Assert.That(picks, Is.EqualTo(new[] { new QuickPickRange("NextWeek", new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 22)) }));
            Assert.That(links, Is.EqualTo(new[] { linked }));
        });
    }

    [Test]
    public async Task EveryRequest_CarriesTheAgentKey()
    {
        await _client.GetTodayAsync(CancellationToken.None);

        var auth = _handler.Requests.Single().Request.Headers.Authorization!;
        Assert.That((auth.Scheme, auth.Parameter), Is.EqualTo(("Bearer", Key)));
    }

    [Test]
    public async Task SetDates_SendsOnlyTheDatesGiven_WithTheExpectedVersionAndActorLabel()
    {
        var id = Guid.NewGuid();

        await _client.SetDatesAsync(id, new DateOnly(2026, 12, 1), null, 9, "Runner when-resolution, confidence high", CancellationToken.None);

        var (request, body) = _handler.Requests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Patch));
            Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo($"/api/work-items/{id}/schedule"));
            Assert.That(request.Headers.GetValues("X-Actor-Label"), Is.EqualTo(new[] { "Runner when-resolution, confidence high" }));
            Assert.That(SameJson(body!, """{"startDate":"2026-12-01","expectedVersion":9}"""), Is.True, body);
        });
    }

    [Test]
    public async Task AddBlockedByLink_PostsABlockedByLink_WithTheActorLabel()
    {
        var id = Guid.NewGuid();
        var blocker = Guid.NewGuid();
        _handler.Respond = _ => StubHttpHandler.Json("{}", HttpStatusCode.Created);

        await _client.AddBlockedByLinkAsync(id, blocker, "Runner when-resolution, confidence low", CancellationToken.None);

        var (request, body) = _handler.Requests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo($"/api/work-items/{id}/links"));
            Assert.That(request.Headers.GetValues("X-Actor-Label"), Is.EqualTo(new[] { "Runner when-resolution, confidence low" }));
            Assert.That(SameJson(body!, $$"""{"targetWorkItemId":"{{blocker}}","type":"BlockedBy"}"""), Is.True, body);
        });
    }

    [TestCase(HttpStatusCode.Conflict)]
    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(HttpStatusCode.NotFound)]
    public void AWriteWeaverTurnsDown_IsRejectedForThatItem(HttpStatusCode status)
    {
        _handler.Respond = _ => StubHttpHandler.Json("""{"title":"Version conflict"}""", status);

        Assert.ThrowsAsync<WeaverRejectedException>(() =>
            _client.SetDatesAsync(Guid.NewGuid(), new DateOnly(2026, 12, 1), null, 1, "label", CancellationToken.None));
    }

    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.InternalServerError)]
    public void ARefusedKeyOrAServerError_MakesWeaverUnavailable_EvenOnAWrite(HttpStatusCode status)
    {
        _handler.Respond = _ => StubHttpHandler.Json("{}", status);

        Assert.ThrowsAsync<WeaverUnavailableException>(() =>
            _client.AddBlockedByLinkAsync(Guid.NewGuid(), Guid.NewGuid(), "label", CancellationToken.None));
    }

    [Test]
    public void AFailedRead_MakesWeaverUnavailable()
    {
        _handler.Respond = _ => StubHttpHandler.Json("{}", HttpStatusCode.BadRequest);

        Assert.ThrowsAsync<WeaverUnavailableException>(() => _client.GetTodayAsync(CancellationToken.None));
    }

    [Test]
    public void AConnectionFailure_MakesWeaverUnavailable()
    {
        _handler.Respond = _ => throw new HttpRequestException("connection refused");

        Assert.ThrowsAsync<WeaverUnavailableException>(() => _client.GetTodayAsync(CancellationToken.None));
    }

    private static bool SameJson(string actual, string expected) =>
        JsonNode.DeepEquals(JsonNode.Parse(actual), JsonNode.Parse(expected));

    private static readonly Guid SpaceId = Guid.NewGuid();

    private static string Item(Guid id, int number) =>
        $$"""
        {"id":"{{id}}","number":{{number}},"title":"Paint","when":"after drywall","spaceId":"{{SpaceId}}",
         "spaceName":"Home","parentId":"{{Guid.NewGuid()}}","parentNumber":40,"parentTitle":"Kitchen",
         "startDate":null,"endDate":"2026-12-06","whenResolved":false,"version":9,"tags":[]}
        """;
}
