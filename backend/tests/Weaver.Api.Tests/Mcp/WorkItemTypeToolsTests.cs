using System.Reflection;
using ModelContextProtocol.Server;
using Moq;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class WorkItemTypeToolsTests
{
    [Test]
    public async Task ListWorkItemTypes_ReturnsEachTypeWithItsOwnFields()
    {
        var types = new Mock<IWorkItemTypeService>(MockBehavior.Strict);
        var fields = new Mock<ICustomFieldService>(MockBehavior.Strict);
        var spaceId = Guid.NewGuid();
        var room = WorkItemType.Create(spaceId, "Room", 0, "#D81B60");
        var chore = WorkItemType.Create(spaceId, "Chore", 1, "#00897B");
        var floor = CustomFieldDefinition.Create(room.Id, "Floor", CustomFieldKind.SingleSelect, 0, ["Oak", "Tiles"]);
        types.Setup(l => l.GetAllAsync(spaceId, It.IsAny<CancellationToken>())).ReturnsAsync([room, chore]);
        fields.Setup(f => f.GetAllAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { room.Id, chore.Id })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([floor]);
        var tools = new WorkItemTypeTools(types.Object, fields.Object);

        var result = await tools.ListWorkItemTypes(spaceId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Select(t => t.Id), Is.EqualTo(new[] { room.Id, chore.Id }));
            Assert.That(result[0].Color, Is.EqualTo("#D81B60"));
            Assert.That(result[0].Fields.Single().Kind, Is.EqualTo(CustomFieldKind.SingleSelect));
            Assert.That(result[0].Fields.Single().Options.Select(o => o.Name), Is.EqualTo(new[] { "Oak", "Tiles" }));
            Assert.That(result[1].Fields, Is.Empty);
        });
    }

    [Test]
    public void NoToolChangesAFieldDefinition()
    {
        var tools = typeof(WorkItemTypeTools).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods())
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .OfType<string>();

        Assert.That(tools.Where(n => n.Contains("field") && !n.StartsWith("list_") && n != "set_work_item_fields"), Is.Empty);
    }
}
