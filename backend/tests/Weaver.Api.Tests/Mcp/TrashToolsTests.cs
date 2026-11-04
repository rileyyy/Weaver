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
public class TrashToolsTests
{
    private Mock<IWorkItemTrashService> _trash = null!;
    private TrashTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        _trash = new Mock<IWorkItemTrashService>(MockBehavior.Strict);
        _tools = new TrashTools(_trash.Object, new HttpCallerContext(new HttpContextAccessor()));
    }

    private static McpServerToolAttribute ToolOf(string method) =>
        typeof(TrashTools).GetMethod(method)!.GetCustomAttribute<McpServerToolAttribute>()!;

    [Test]
    public async Task DeleteWorkItem_MovesToTheTrash_PassingCascadeAndExpectedVersion()
    {
        var id = Guid.NewGuid();
        _trash.Setup(s => s.MoveToTrashAsync(id, true, 7u, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _tools.DeleteWorkItem(id, cascade: true, expectedVersion: 7u, ct: CancellationToken.None);

        _trash.VerifyAll();
    }

    [Test]
    public void DeleteWorkItem_WhenHasChildrenAndCascadeFalse_ThrowsMcpExceptionWithSameMessage()
    {
        var id = Guid.NewGuid();
        var domainException = new WorkItemHasChildrenException(id);
        _trash.Setup(s => s.MoveToTrashAsync(id, false, null, It.IsAny<CancellationToken>())).ThrowsAsync(domainException);

        var thrown = Assert.ThrowsAsync<McpException>(() => _tools.DeleteWorkItem(id, ct: CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    [Test]
    public async Task ListTrash_MapsEachDelete()
    {
        var id = Guid.NewGuid();
        _trash.Setup(s => s.ListAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync([TrashControllerTests.MakeTrashed(id)]);

        var result = await _tools.ListTrash(ct: CancellationToken.None);

        Assert.That(result.Single().Item.Id, Is.EqualTo(id));
    }

    [Test]
    public async Task RestoreWorkItem_ReturnsTheRestoredItem()
    {
        var item = WorkItem.Create(Guid.NewGuid(), "Old shed", null, Guid.NewGuid(), rank: 1.0);
        _trash.Setup(s => s.RestoreAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _tools.RestoreWorkItem(item.Id, ct: CancellationToken.None);

        Assert.That(result.Id, Is.EqualTo(item.Id));
    }

    // ADR-0007: read-only keys can see the Trash; only read-write ones can change it.
    [Test]
    public void ListTrash_IsReadOnly_AndTheWritesAreNot()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ToolOf(nameof(TrashTools.ListTrash)).ReadOnly, Is.True);
            Assert.That(ToolOf(nameof(TrashTools.RestoreWorkItem)).ReadOnly, Is.False);
            Assert.That(ToolOf(nameof(TrashTools.DeleteWorkItem)).ReadOnly, Is.False);
        });
    }
}
