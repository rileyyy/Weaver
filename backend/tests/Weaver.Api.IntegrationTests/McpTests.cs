using System.Net;
using ModelContextProtocol.Client;

namespace Weaver.Api.IntegrationTests;

[TestFixture]
public class McpTests
{
    [Test]
    public async Task Mcp_WithoutToken_Returns401()
    {
        var response = await ApiClient.Anonymous().PostAsync("/mcp", new StringContent("{}"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Mcp_WithToken_ListsAndCallsTools()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(client.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            client,
            loggerFactory: null,
            ownsHttpClient: false);
        await using var mcp = await McpClient.CreateAsync(transport);

        var tools = await mcp.ListToolsAsync();
        var result = await mcp.CallToolAsync("list_statuses", new Dictionary<string, object?>());

        Assert.Multiple(() =>
        {
            Assert.That(tools.Select(t => t.Name), Does.Contain("create_work_item"));
            Assert.That(result.IsError, Is.Not.True);
        });
    }
}
