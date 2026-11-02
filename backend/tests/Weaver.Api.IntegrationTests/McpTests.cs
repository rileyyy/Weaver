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
        await using var mcp = await ConnectAsync(client);

        var tools = await mcp.ListToolsAsync();
        var result = await mcp.CallToolAsync("list_statuses", new Dictionary<string, object?>());
        var allItems = await mcp.CallToolAsync("list_all_work_items", new Dictionary<string, object?>());

        Assert.Multiple(() =>
        {
            Assert.That(tools.Select(t => t.Name), Does.Contain("create_work_item").And.Contain("list_all_work_items"));
            Assert.That(result.IsError, Is.Not.True);
            Assert.That(allItems.IsError, Is.Not.True);
        });
    }

    // Agents may read the server settings but never change them (ADR-0004).
    [Test]
    public async Task Mcp_ServerSettings_AreReadableButNotWritable()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        await using var mcp = await ConnectAsync(client);

        var tools = await mcp.ListToolsAsync();
        var result = await mcp.CallToolAsync("get_server_settings", new Dictionary<string, object?>());

        var settingsTools = tools.Select(t => t.Name).Where(n => n.Contains("setting") || n.Contains("time_zone"));
        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.Not.True);
            Assert.That(settingsTools, Is.EqualTo(new[] { "get_server_settings" }));
        });
    }

    private static Task<McpClient> ConnectAsync(HttpClient client) =>
        McpClient.CreateAsync(new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(client.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            client,
            loggerFactory: null,
            ownsHttpClient: false));
}
