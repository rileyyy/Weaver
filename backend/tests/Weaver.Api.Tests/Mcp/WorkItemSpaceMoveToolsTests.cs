using System.Reflection;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Moq;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class WorkItemSpaceMoveToolsTests
{
    private Mock<IWorkItemSpaceMoveService> _moves = null!;
    private WorkItemSpaceMoveTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        _moves = new Mock<IWorkItemSpaceMoveService>(MockBehavior.Strict);
        _tools = new WorkItemSpaceMoveTools(
            _moves.Object,
            new WorkItemDtoMapper(new FakeWorkItemBlockers(), new FakeWorkItemNameLookup()),
            new HttpCallerContext(new HttpContextAccessor()));
    }

    private static McpServerToolAttribute ToolOf(string method) =>
        typeof(WorkItemSpaceMoveTools).GetMethod(method)!.GetCustomAttribute<McpServerToolAttribute>()!;

    [Test]
    public async Task PlanWorkItemSpaceMove_ReturnsThePlan()
    {
        var id = Guid.NewGuid();
        var target = Guid.NewGuid();
        _moves.Setup(s => s.PlanAsync(id, target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkItemSpaceMoveControllerTests.MakePlan(id, target));

        var plan = await _tools.PlanWorkItemSpaceMove(id, target, CancellationToken.None);

        Assert.That(plan.Statuses.Single().TargetName, Is.EqualTo("Quoted"));
    }

    [Test]
    public async Task MoveWorkItemToSpace_PassesTheMapsThrough_TreatingAMissingMapAsNone()
    {
        var item = WorkItem.Create(Guid.NewGuid(), "Shed", null, Guid.NewGuid(), rank: 1.0);
        var (target, high, urgent) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _moves.Setup(s => s.MoveAsync(
                item.Id,
                target,
                null,
                It.Is<SpaceMoveMaps>(m => m.Statuses.Count == 0 && m.Types.Count == 0 && m.Priorities[high] == urgent),
                3u,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, [WorkItemField.SpaceId]));

        var response = await _tools.MoveWorkItemToSpace(
            item.Id,
            target,
            priorityMap: new Dictionary<Guid, Guid?> { [high] = urgent },
            expectedVersion: 3u,
            ct: CancellationToken.None);

        Assert.That(response.ChangedFields, Is.EqualTo(new[] { WorkItemField.SpaceId }));
        _moves.VerifyAll();
    }

    [Test]
    public void MoveWorkItemToSpace_WhenRejected_ThrowsMcpExceptionWithTheSameMessage()
    {
        var id = Guid.NewGuid();
        var rejection = new CrossSpaceParentException(id, Guid.NewGuid());
        _moves.Setup(s => s.MoveAsync(id, It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<SpaceMoveMaps>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(rejection);

        var thrown = Assert.ThrowsAsync<McpException>(() => _tools.MoveWorkItemToSpace(id, Guid.NewGuid(), ct: CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(rejection.Message));
    }

    // Only read-write keys can move: the write filter lets through only tools marked read-only.
    [Test]
    public void ThePlanIsReadOnly_AndTheMoveIsADestructiveWrite()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ToolOf(nameof(WorkItemSpaceMoveTools.PlanWorkItemSpaceMove)).ReadOnly, Is.True);
            Assert.That(ToolOf(nameof(WorkItemSpaceMoveTools.MoveWorkItemToSpace)).ReadOnly, Is.False);
            Assert.That(ToolOf(nameof(WorkItemSpaceMoveTools.MoveWorkItemToSpace)).Destructive, Is.True);
        });
    }
}
