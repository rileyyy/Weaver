using System.Net;
using System.Text.Json.Nodes;
using Weaver.Runner.Model;
using Weaver.Runner.Tests.Fakes;

namespace Weaver.Runner.Tests.Model;

[TestFixture]
public class OpenAiCompatibleModelClientTests
{
    private static readonly ModelPrompt Prompt = new("Answer in JSON.", "Work item: #1 Paint");
    private StubHttpHandler _handler = null!;

    [SetUp]
    public void SetUp() => _handler = new StubHttpHandler
    {
        Respond = _ => StubHttpHandler.Json("""{"choices":[{"message":{"role":"assistant","content":"{\"skip\":\"ambiguous\"}"}}]}"""),
    };

    [TearDown]
    public void TearDown() => _handler.Dispose();

    [Test]
    public async Task AsksTheChatCompletionsEndpoint_ForDeterministicJson_AndReturnsTheReply()
    {
        var reply = await Client(apiKey: null, jsonMode: true).CompleteAsync(Prompt, CancellationToken.None);

        var (request, body) = _handler.Requests.Single();
        var expected = JsonNode.Parse("""
            {"model":"llama3.1:8b","temperature":0,"stream":false,
             "messages":[{"role":"system","content":"Answer in JSON."},{"role":"user","content":"Work item: #1 Paint"}],
             "response_format":{"type":"json_object"}}
            """);
        Assert.Multiple(() =>
        {
            Assert.That(reply, Is.EqualTo("""{"skip":"ambiguous"}"""));
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(request.RequestUri, Is.EqualTo(new Uri("http://ollama.test:11434/v1/chat/completions")));
            Assert.That(request.Headers.Authorization, Is.Null);
            Assert.That(JsonNode.DeepEquals(JsonNode.Parse(body!), expected), Is.True, body);
        });
    }

    [Test]
    public async Task WithAKey_SignsIn_AndWithoutJsonMode_LeavesTheResponseFormatOut()
    {
        await Client(apiKey: "sk-test", jsonMode: false).CompleteAsync(Prompt, CancellationToken.None);

        var (request, body) = _handler.Requests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(request.Headers.Authorization?.ToString(), Is.EqualTo("Bearer sk-test"));
            Assert.That(JsonNode.Parse(body!)!.AsObject().ContainsKey("response_format"), Is.False);
        });
    }

    [Test]
    public async Task AReplyWithNoContent_IsReturnedEmpty_ForTheCheckerToReject()
    {
        _handler.Respond = _ => StubHttpHandler.Json("""{"choices":[]}""");

        Assert.That(await Client().CompleteAsync(Prompt, CancellationToken.None), Is.Empty);
    }

    [TestCase(HttpStatusCode.NotFound, TestName = "UnknownModel")]
    [TestCase(HttpStatusCode.Unauthorized, TestName = "KeyRefused")]
    [TestCase(HttpStatusCode.InternalServerError, TestName = "ServerError")]
    public void AnErrorStatus_MakesTheModelUnavailable(HttpStatusCode status)
    {
        _handler.Respond = _ => StubHttpHandler.Json("""{"error":"model not found"}""", status);

        Assert.ThrowsAsync<ModelUnavailableException>(() => Client().CompleteAsync(Prompt, CancellationToken.None));
    }

    [Test]
    public void AConnectionFailure_MakesTheModelUnavailable()
    {
        _handler.Respond = _ => throw new HttpRequestException("connection refused");

        Assert.ThrowsAsync<ModelUnavailableException>(() => Client().CompleteAsync(Prompt, CancellationToken.None));
    }

    [Test]
    public void ATimeout_MakesTheModelUnavailable()
    {
        _handler.Respond = _ => throw new TaskCanceledException("timed out");

        Assert.ThrowsAsync<ModelUnavailableException>(() => Client().CompleteAsync(Prompt, CancellationToken.None));
    }

    private OpenAiCompatibleModelClient Client(string? apiKey = null, bool jsonMode = true) =>
        new(new HttpClient(_handler) { BaseAddress = new Uri("http://ollama.test:11434/v1/") }, "llama3.1:8b", apiKey, jsonMode);
}
