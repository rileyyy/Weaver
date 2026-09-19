using Moq;
using Weaver.Api.Mcp;
using Weaver.Domain;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class UserToolsTests
{
    [Test]
    public async Task ListUsers_DelegatesToServiceAndMapsResults()
    {
        var users = new Mock<IUserService>(MockBehavior.Strict);
        var user = new User { Id = Guid.NewGuid(), Username = "alice", NormalizedUsername = "alice", PasswordHash = "hash" };
        users.Setup(u => u.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([user]);
        var tools = new UserTools(users.Object);

        var result = await tools.ListUsers(CancellationToken.None);

        Assert.That(result.Single().Id, Is.EqualTo(user.Id));
    }
}
