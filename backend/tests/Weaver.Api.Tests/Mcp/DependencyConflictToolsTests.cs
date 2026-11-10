using System.Reflection;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class DependencyConflictToolsTests
{
    [Test]
    public async Task ListDependencyConflicts_PassesTheSpaceThrough_AndNamesBothItems()
    {
        var spaceId = Guid.NewGuid();
        var blocked = new DependencyConflictItem(Guid.NewGuid(), 8, "Paint", spaceId, "Home", new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 8));
        var blocker = new DependencyConflictItem(Guid.NewGuid(), 3, "Drywall", Guid.NewGuid(), "Garage", new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 6));
        var service = new Mock<IDependencyConflicts>(MockBehavior.Strict);
        service
            .Setup(s => s.ListAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DependencyConflict(blocked, blocker)]);

        var conflicts = await new DependencyConflictTools(service.Object).ListDependencyConflicts(spaceId);

        Assert.That(conflicts, Is.EqualTo(new[]
        {
            new DependencyConflictDto(
                new DependencyConflictItemDto(blocked.Id, 8, "Paint", spaceId, "Home", new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 8)),
                new DependencyConflictItemDto(blocker.Id, 3, "Drywall", blocker.SpaceId, "Garage", new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 6))),
        }));
    }

    [Test]
    public void ListDependencyConflicts_ForAnUnknownSpace_ReportsAToolError()
    {
        var spaceId = Guid.NewGuid();
        var service = new Mock<IDependencyConflicts>(MockBehavior.Strict);
        service
            .Setup(s => s.ListAsync(spaceId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException(nameof(Space), spaceId));

        var ex = Assert.ThrowsAsync<McpException>(() => new DependencyConflictTools(service.Object).ListDependencyConflicts(spaceId));

        Assert.That(ex!.Message, Does.Contain(spaceId.ToString()));
    }

    [Test]
    public void ListDependencyConflicts_IsReadOnly_SoReadOnlyKeysCanAskIt()
    {
        var tool = typeof(DependencyConflictTools).GetMethod(nameof(DependencyConflictTools.ListDependencyConflicts))!
            .GetCustomAttribute<McpServerToolAttribute>()!;

        Assert.That(tool.ReadOnly, Is.True);
    }
}
