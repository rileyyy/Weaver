using System.Reflection;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Moq;
using Weaver.Api.Auth;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class WorkItemLinkToolsTests
{
    private Mock<IWorkItemLinkService> _links = null!;
    private WorkItemLinkTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        _links = new Mock<IWorkItemLinkService>(MockBehavior.Strict);
        _tools = new WorkItemLinkTools(_links.Object, new HttpCallerContext(new HttpContextAccessor()));
    }

    private static WorkItemLinkView MakeLink(Guid linkedWorkItemId, WorkItemLinkRelation type = WorkItemLinkRelation.Related) =>
        new(Guid.NewGuid(), type, linkedWorkItemId, 7, "B", Guid.NewGuid());

    [Test]
    public async Task ListWorkItemLinks_DelegatesToServiceAndMapsResults()
    {
        var workItemId = Guid.NewGuid();
        var link = MakeLink(Guid.NewGuid(), WorkItemLinkRelation.BlockedBy);
        _links.Setup(l => l.ListForWorkItemAsync(workItemId, It.IsAny<CancellationToken>())).ReturnsAsync([link]);

        var result = await _tools.ListWorkItemLinks(workItemId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Single().LinkedWorkItemTitle, Is.EqualTo("B"));
            Assert.That(result.Single().LinkedWorkItemNumber, Is.EqualTo(7));
            Assert.That(result.Single().Type, Is.EqualTo(WorkItemLinkRelation.BlockedBy));
        });
    }

    [Test]
    public async Task CreateWorkItemLink_PassesTheTypeToTheService()
    {
        var workItemId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        _links.Setup(l => l.CreateAsync(workItemId, targetId, WorkItemLinkRelation.Duplicates, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeLink(targetId, WorkItemLinkRelation.Duplicates));

        var result = await _tools.CreateWorkItemLink(workItemId, targetId, WorkItemLinkRelation.Duplicates, ct: CancellationToken.None);

        Assert.That(result.Type, Is.EqualTo(WorkItemLinkRelation.Duplicates));
    }

    [Test]
    public void CreateWorkItemLink_WhenSelfLink_ThrowsMcpExceptionWithSameMessage()
    {
        var workItemId = Guid.NewGuid();
        var domainException = new SelfWorkItemLinkException(workItemId);
        _links.Setup(l => l.CreateAsync(workItemId, workItemId, WorkItemLinkRelation.Related, It.IsAny<CancellationToken>()))
            .ThrowsAsync(domainException);

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            _tools.CreateWorkItemLink(workItemId, workItemId, WorkItemLinkRelation.Related, ct: CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    [Test]
    public void CreateWorkItemLink_WhenABlockingCycle_ThrowsMcpExceptionWithSameMessage()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var domainException = new BlockingCycleException(a, b);
        _links.Setup(l => l.CreateAsync(a, b, WorkItemLinkRelation.Blocks, It.IsAny<CancellationToken>())).ThrowsAsync(domainException);

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            _tools.CreateWorkItemLink(a, b, WorkItemLinkRelation.Blocks, ct: CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    [Test]
    public async Task DeleteWorkItemLink_DelegatesToService()
    {
        var id = Guid.NewGuid();
        _links.Setup(l => l.DeleteAsync(id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _tools.DeleteWorkItemLink(id, ct: CancellationToken.None);

        _links.VerifyAll();
    }

    [Test]
    public void TheTypeArgument_IsRequired()
    {
        var type = typeof(WorkItemLinkTools).GetMethod(nameof(WorkItemLinkTools.CreateWorkItemLink))!
            .GetParameters().Single(p => p.Name == "type");

        Assert.That(type.HasDefaultValue, Is.False);
    }

    [Test]
    public void ListIsReadOnly_CreateIsNotDestructive_DeleteIsDestructive()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ToolOf(nameof(WorkItemLinkTools.ListWorkItemLinks)).ReadOnly, Is.True);
            Assert.That(ToolOf(nameof(WorkItemLinkTools.CreateWorkItemLink)).Destructive, Is.False);
            Assert.That(ToolOf(nameof(WorkItemLinkTools.DeleteWorkItemLink)).Destructive, Is.True);
        });
    }

    private static McpServerToolAttribute ToolOf(string method) =>
        typeof(WorkItemLinkTools).GetMethod(method)!.GetCustomAttribute<McpServerToolAttribute>()!;
}
