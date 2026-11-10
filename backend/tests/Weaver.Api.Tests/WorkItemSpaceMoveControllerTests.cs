using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

[TestFixture]
public class WorkItemSpaceMoveControllerTests
{
    private Mock<IWorkItemSpaceMoveService> _moves = null!;
    private WorkItemSpaceMoveController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _moves = new Mock<IWorkItemSpaceMoveService>(MockBehavior.Strict);
        _controller = new WorkItemSpaceMoveController(
            _moves.Object, new WorkItemDtoMapper(new FakeWorkItemBlockers(), new FakeWorkItemNameLookup()));
    }

    public static SpaceMovePlan MakePlan(Guid id, Guid targetSpaceId) => new(
        id,
        targetSpaceId,
        ItemCount: 3,
        TrashedItemCount: 1,
        ClearedFieldValueCount: 2,
        Statuses: [new SpaceMoveMapping(Guid.NewGuid(), "To Do", Guid.NewGuid(), "Quoted", 3)],
        Types: [new SpaceMoveMapping(Guid.NewGuid(), "Room", null, null, 1)],
        Priorities: []);

    [Test]
    public async Task Plan_ReturnsTheMappingForTheTargetSpace()
    {
        var id = Guid.NewGuid();
        var target = Guid.NewGuid();
        _moves.Setup(s => s.PlanAsync(id, target, It.IsAny<CancellationToken>())).ReturnsAsync(MakePlan(id, target));

        var result = await _controller.Plan(id, target);

        var dto = (SpaceMovePlanDto)((OkObjectResult)result.Result!).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(dto.ItemCount, Is.EqualTo(3));
            Assert.That(dto.ClearedFieldValueCount, Is.EqualTo(2));
            Assert.That(dto.Statuses.Single().TargetName, Is.EqualTo("Quoted"));
            Assert.That(dto.Types.Single().TargetId, Is.Null);
        });
    }

    [Test]
    public async Task Move_PassesTheTargetParentMapsAndVersionThrough()
    {
        var item = TestWorkItems.Create("Shed");
        var (target, parent, toDo, quoted, room) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _moves.Setup(s => s.MoveAsync(
                item.Id,
                target,
                parent,
                It.Is<SpaceMoveMaps>(m => m.Statuses[toDo] == quoted && m.Types[room] == null && m.Priorities.Count == 0),
                7u,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemWriteResult(item, [WorkItemField.SpaceId]));

        var result = await _controller.Move(item.Id, new MoveWorkItemToSpaceRequest(
            target,
            parent,
            StatusMap: new Dictionary<Guid, Guid> { [toDo] = quoted },
            TypeMap: new Dictionary<Guid, Guid?> { [room] = null },
            PriorityMap: null,
            ExpectedVersion: 7u));

        var response = (WorkItemWriteResponse)((OkObjectResult)result.Result!).Value!;
        Assert.That(response.ChangedFields, Is.EqualTo(new[] { WorkItemField.SpaceId }));
        _moves.VerifyAll();
    }
}
