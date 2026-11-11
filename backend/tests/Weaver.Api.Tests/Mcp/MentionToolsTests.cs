using System.Reflection;
using ModelContextProtocol.Server;
using Moq;
using Weaver.Api.Mcp;
using Weaver.Application.Services;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class MentionToolsTests
{
    [Test]
    public async Task ListPendingMentions_ReturnsTheCallersPendingMentions()
    {
        var pending = MentionsControllerTests.MakePending();
        var mentions = new Mock<IMentionService>(MockBehavior.Strict);
        mentions.Setup(m => m.ListPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync([pending]);
        var tools = new MentionTools(mentions.Object);

        var result = await tools.ListPendingMentions(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Single().Comment.Id, Is.EqualTo(pending.Comment.Id));
            Assert.That(result.Single().WorkItemNumber, Is.EqualTo(42));
        });
    }

    /// <summary>Read-only keys are only allowed tools marked ReadOnly, and agents on one must still see their mentions.</summary>
    [Test]
    public void ListPendingMentions_IsAReadOnlyTool()
    {
        var attribute = typeof(MentionTools)
            .GetMethod(nameof(MentionTools.ListPendingMentions))!
            .GetCustomAttribute<McpServerToolAttribute>()!;

        Assert.Multiple(() =>
        {
            Assert.That(attribute.Name, Is.EqualTo("list_pending_mentions"));
            Assert.That(attribute.ReadOnly, Is.True);
        });
    }
}
