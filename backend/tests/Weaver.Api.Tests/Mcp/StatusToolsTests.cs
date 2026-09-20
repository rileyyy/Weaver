using Moq;
using Weaver.Api.Mcp;
using Weaver.Domain;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class StatusToolsTests
{
    [Test]
    public async Task ListStatuses_DelegatesToServiceAndMapsResults()
    {
        var statuses = new Mock<IStatusService>(MockBehavior.Strict);
        var status = new Status { Id = Guid.NewGuid(), Name = "To Do", Order = 0, Category = StatusCategory.ToDo, Color = "#1E88E5" };
        statuses.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([status]);
        var tools = new StatusTools(statuses.Object);

        var result = await tools.ListStatuses(CancellationToken.None);

        Assert.That(result.Single().Id, Is.EqualTo(status.Id));
    }
}
