using System.Reflection;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Moq;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class WorkItemUrlLinkToolsTests
{
    private Mock<IWorkItemUrlLinkService> _urlLinks = null!;
    private WorkItemUrlLinkTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        _urlLinks = new Mock<IWorkItemUrlLinkService>(MockBehavior.Strict);
        _tools = new WorkItemUrlLinkTools(_urlLinks.Object, new HttpCallerContext(new HttpContextAccessor()));
    }

    [Test]
    public async Task ListWorkItemUrlLinks_DelegatesToServiceAndMapsResults()
    {
        var workItemId = Guid.NewGuid();
        var link = new WorkItemUrlLinkView(Guid.NewGuid(), "https://example.com", "Quote");
        _urlLinks.Setup(l => l.ListForWorkItemAsync(workItemId, It.IsAny<CancellationToken>())).ReturnsAsync([link]);

        var result = await _tools.ListWorkItemUrlLinks(workItemId, CancellationToken.None);

        Assert.That(result.Single(), Is.EqualTo(new WorkItemUrlLinkDto(link.Id, "https://example.com", "Quote")));
    }

    [Test]
    public async Task CreateWorkItemUrlLink_DelegatesToService()
    {
        var workItemId = Guid.NewGuid();
        var link = new WorkItemUrlLinkView(Guid.NewGuid(), "https://example.com", "Quote");
        _urlLinks.Setup(l => l.CreateAsync(workItemId, "https://example.com", "Quote", It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);

        var result = await _tools.CreateWorkItemUrlLink(workItemId, "https://example.com", "Quote", ct: CancellationToken.None);

        Assert.That(result.Id, Is.EqualTo(link.Id));
    }

    [Test]
    public void CreateWorkItemUrlLink_WhenInvalid_ThrowsMcpExceptionWithSameMessage()
    {
        var workItemId = Guid.NewGuid();
        var domainException = new DomainValidationException("URL must be an absolute http or https address.");
        _urlLinks.Setup(l => l.CreateAsync(workItemId, "nope", null, It.IsAny<CancellationToken>())).ThrowsAsync(domainException);

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            _tools.CreateWorkItemUrlLink(workItemId, "nope", ct: CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    [Test]
    public async Task DeleteWorkItemUrlLink_DelegatesToService()
    {
        var id = Guid.NewGuid();
        _urlLinks.Setup(l => l.DeleteAsync(id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _tools.DeleteWorkItemUrlLink(id, ct: CancellationToken.None);

        _urlLinks.VerifyAll();
    }

    [Test]
    public void ListIsReadOnly_CreateIsNotDestructive_DeleteIsDestructive()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ToolOf(nameof(WorkItemUrlLinkTools.ListWorkItemUrlLinks)).ReadOnly, Is.True);
            Assert.That(ToolOf(nameof(WorkItemUrlLinkTools.CreateWorkItemUrlLink)).Destructive, Is.False);
            Assert.That(ToolOf(nameof(WorkItemUrlLinkTools.DeleteWorkItemUrlLink)).Destructive, Is.True);
        });
    }

    private static McpServerToolAttribute ToolOf(string method) =>
        typeof(WorkItemUrlLinkTools).GetMethod(method)!.GetCustomAttribute<McpServerToolAttribute>()!;
}
