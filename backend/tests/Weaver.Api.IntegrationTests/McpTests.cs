using System.Net;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Weaver.Api.Contracts;
using Weaver.Domain;

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

    [Test]
    public async Task Mcp_WithAnApiKey_WhoamiNamesTheAgentTheKeyAndItsScope()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var agentName = $"Claude Code ({Guid.NewGuid():N})"[..40];
        var agent = await (await admin.PostJsonAsync("/api/admin/agents", new CreateAgentAccountRequest(agentName, null)))
            .ReadAsync<AgentAccountDto>();
        var issued = await (await admin.PostJsonAsync(
                $"/api/admin/agents/{agent.Id}/keys",
                new CreateApiKeyRequest("laptop", ApiKeyScope.ReadOnly)))
            .ReadAsync<IssuedApiKeyDto>();
        await using var mcp = await ConnectAsync(ApiClient.WithApiKey(issued.Key));

        var whoami = await CallAsync<WhoAmIDto>(mcp, "whoami");

        Assert.That(whoami, Is.EqualTo(new WhoAmIDto(agentName, UserKind.Agent, "laptop", ApiKeyScope.ReadOnly)));
    }

    [Test]
    public async Task Mcp_WithAnAccessToken_WhoamiNamesTheHumanAndNoKey()
    {
        var (client, auth) = await ApiClient.SignedInAsync();
        await using var mcp = await ConnectAsync(client);

        var whoami = await CallAsync<WhoAmIDto>(mcp, "whoami");

        Assert.That(whoami, Is.EqualTo(new WhoAmIDto(auth.User.Username, UserKind.Human, KeyLabel: null, Scope: null)));
    }

    [Test]
    public async Task Mcp_WithARevokedKey_Returns401()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        var agent = await (await admin.PostJsonAsync(
                "/api/admin/agents",
                new CreateAgentAccountRequest($"Revoked ({Guid.NewGuid():N})"[..40], null)))
            .ReadAsync<AgentAccountDto>();
        var issued = await (await admin.PostJsonAsync(
                $"/api/admin/agents/{agent.Id}/keys",
                new CreateApiKeyRequest("laptop", ApiKeyScope.ReadWrite)))
            .ReadAsync<IssuedApiKeyDto>();
        await admin.DeleteAsync($"/api/admin/agents/{agent.Id}/keys/{issued.ApiKey.Id}");

        var response = await ApiClient.WithApiKey(issued.Key).PostAsync("/mcp", new StringContent("{}"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private static async Task<T> CallAsync<T>(McpClient mcp, string tool)
    {
        var result = await mcp.CallToolAsync(tool, new Dictionary<string, object?>());
        Assert.That(result.IsError, Is.Not.True, () => string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text)));
        return JsonSerializer.Deserialize<T>(result.Content.OfType<TextContentBlock>().Single().Text, ApiClient.Json)!;
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
