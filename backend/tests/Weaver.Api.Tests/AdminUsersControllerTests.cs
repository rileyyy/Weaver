using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

[TestFixture]
public class AdminUsersControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private Mock<IUserAdministrationService> _users = null!;
    private AdminUsersController _controller = null!;
    private Guid _adminId;

    [SetUp]
    public void SetUp()
    {
        _users = new Mock<IUserAdministrationService>(MockBehavior.Strict);
        _adminId = Guid.NewGuid();
        _controller = new AdminUsersController(_users.Object, new FixedClock(Now))
        {
            ControllerContext = SignedInControllerContext.For(_adminId),
        };
    }

    [Test]
    public async Task GetAll_ReportsActiveAndLockoutState()
    {
        var locked = User.CreateHuman("locked", "locked");
        for (var i = 0; i < User.MaxFailedLoginAttempts; i++)
        {
            locked.RecordFailedLogin(Now);
        }
        var inactive = User.CreateHuman("inactive", "inactive", isAdmin: true);
        inactive.Deactivate(Now);
        _users.Setup(u => u.ListAsync(_adminId, It.IsAny<CancellationToken>())).ReturnsAsync([locked, inactive]);

        var result = await _controller.GetAll();

        var dtos = ((result.Result as OkObjectResult)!.Value as IEnumerable<AdminUserDto>)!.ToList();
        Assert.Multiple(() =>
        {
            Assert.That(dtos[0].LockedUntilUtc, Is.EqualTo(Now + User.LockoutDuration));
            Assert.That(dtos[0].IsActive, Is.True);
            Assert.That(dtos[1].IsActive, Is.False);
            Assert.That(dtos[1].IsAdmin, Is.True);
            Assert.That(dtos[1].LockedUntilUtc, Is.Null);
        });
    }

    [Test]
    public async Task Create_PassesTheCallerAndReturns201()
    {
        var created = User.CreateHuman("bob", "bob");
        _users.Setup(u => u.CreateAsync(_adminId, "bob", "a long password", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _controller.Create(new CreateUserRequest("bob", "a long password", false));

        Assert.That(((result.Result as CreatedAtActionResult)!.Value as AdminUserDto)!.Id, Is.EqualTo(created.Id));
    }

    [Test]
    public async Task FieldUpdates_PassTheCallerAndTarget()
    {
        var target = User.CreateHuman("bob", "bob");
        _users.Setup(u => u.RenameAsync(_adminId, target.Id, "rob", It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _users.Setup(u => u.ResetPasswordAsync(_adminId, target.Id, "a long password", It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _users.Setup(u => u.UnlockAsync(_adminId, target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _users.Setup(u => u.SetAdminAsync(_adminId, target.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _users.Setup(u => u.SetActiveAsync(_adminId, target.Id, false, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var results = new[]
        {
            (await _controller.Rename(target.Id, new RenameUserRequest("rob"))).Result,
            (await _controller.ResetPassword(target.Id, new ResetPasswordRequest("a long password"))).Result,
            (await _controller.Unlock(target.Id)).Result,
            (await _controller.SetAdmin(target.Id, new SetUserAdminRequest(true))).Result,
            (await _controller.SetActive(target.Id, new SetUserActiveRequest(false))).Result,
        };

        Assert.That(results, Is.All.TypeOf<OkObjectResult>());
        _users.VerifyAll();
    }

    [Test]
    public async Task Delete_Returns204()
    {
        var id = Guid.NewGuid();
        _users.Setup(u => u.DeleteAsync(_adminId, id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.Delete(id);

        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
