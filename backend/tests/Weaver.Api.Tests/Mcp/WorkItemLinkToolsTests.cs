using ModelContextProtocol;
using Moq;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
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
        _tools = new WorkItemLinkTools(_links.Object);
    }

    private static WorkItemLinkView MakeLink(Guid linkedWorkItemId) => new(Guid.NewGuid(), linkedWorkItemId, "B");

    [Test]
    public async Task ListWorkItemLinks_DelegatesToServiceAndMapsResults()
    {
        var workItemId = Guid.NewGuid();
        var link = MakeLink(Guid.NewGuid());
        _links.Setup(l => l.ListForWorkItemAsync(workItemId, It.IsAny<CancellationToken>())).ReturnsAsync([link]);

        var result = await _tools.ListWorkItemLinks(workItemId, CancellationToken.None);

        Assert.That(result.Single().LinkedWorkItemTitle, Is.EqualTo("B"));
    }

    [Test]
    public void CreateWorkItemLink_WhenSelfLink_ThrowsMcpExceptionWithSameMessage()
    {
        var workItemId = Guid.NewGuid();
        var domainException = new SelfWorkItemLinkException(workItemId);
        _links.Setup(l => l.CreateAsync(workItemId, workItemId, It.IsAny<CancellationToken>())).ThrowsAsync(domainException);

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            _tools.CreateWorkItemLink(workItemId, workItemId, CancellationToken.None));

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
}
