using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

[TestFixture]
public class WorkItemRollUpsControllerTests
{
    [Test]
    public async Task Get_ReturnsEachTotalWithItsFieldsNameTypeAndKind()
    {
        var typeId = Guid.NewGuid();
        var budget = CustomFieldDefinition.Create(typeId, "Budget", CustomFieldKind.Currency, 0, rollsUp: true);
        var itemId = Guid.NewGuid();
        var rollUps = new Mock<IWorkItemRollUpService>(MockBehavior.Strict);
        rollUps.Setup(r => r.GetAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RollUpTotal(budget, 350.5m)]);
        var controller = new WorkItemRollUpsController(rollUps.Object);

        var result = await controller.Get(itemId);

        var dtos = (result.Result as OkObjectResult)!.Value as IEnumerable<RollUpDto>;
        Assert.That(dtos, Is.EqualTo(new[] { new RollUpDto(budget.Id, "Budget", typeId, CustomFieldKind.Currency, 350.5m) }));
    }
}
