using System.Reflection;
using ModelContextProtocol.Server;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class ServerSettingsToolsTests
{
    [Test]
    public async Task GetServerSettings_ReturnsTheTimeZone()
    {
        var settings = new Mock<IServerSettingsService>(MockBehavior.Strict);
        var stored = ServerSettings.CreateDefault();
        stored.SetTimeZone("Asia/Tokyo");
        settings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        var tools = new ServerSettingsTools(settings.Object);

        var result = await tools.GetServerSettings(CancellationToken.None);

        Assert.That(result, Is.EqualTo(new ServerSettingsDto("Asia/Tokyo", 30)));
    }

    // Agents may read admin settings but never change them (ADR-0004).
    [Test]
    public void ServerSettingsTools_OnlyExposeReadOnlyTools()
    {
        var tools = typeof(ServerSettingsTools).GetMethods()
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>())
            .OfType<McpServerToolAttribute>()
            .ToList();

        Assert.That(tools, Is.Not.Empty);
        Assert.That(tools, Is.All.Matches<McpServerToolAttribute>(t => t.ReadOnly));
    }
}
