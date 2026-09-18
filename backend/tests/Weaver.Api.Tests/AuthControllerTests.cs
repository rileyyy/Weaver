using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Domain;
using Weaver.Infrastructure.Auth;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Tests;

[TestFixture]
public class AuthControllerTests
{
    private Mock<IAuthService> _auth = null!;
    private AuthController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _auth = new Mock<IAuthService>(MockBehavior.Strict);
        _controller = new AuthController(_auth.Object);
    }

    private static User MakeUser() => new()
    {
        Id = Guid.NewGuid(),
        Username = "alice",
        NormalizedUsername = "alice",
        PasswordHash = "hashed",
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

    private static AuthResult MakeAuthResult(User user) => new(
        user,
        new AccessToken("access-token", DateTimeOffset.UtcNow.AddMinutes(15)),
        "refresh-token");

    [Test]
    public async Task Register_DelegatesToServiceAndReturnsOkWithTokens()
    {
        var user = MakeUser();
        _auth.Setup(a => a.RegisterAsync("alice", "a valid password", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAuthResult(user));

        var result = await _controller.Register(new RegisterRequest("alice", "a valid password"));

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var body = ok!.Value as AuthResponse;
        Assert.That(body!.User.Username, Is.EqualTo("alice"));
        Assert.That(body.RefreshToken, Is.EqualTo("refresh-token"));
    }

    [Test]
    public async Task Login_DelegatesToServiceAndReturnsOkWithTokens()
    {
        var user = MakeUser();
        _auth.Setup(a => a.LoginAsync("alice", "a valid password", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAuthResult(user));

        var result = await _controller.Login(new LoginRequest("alice", "a valid password"));

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That((ok!.Value as AuthResponse)!.AccessToken, Is.EqualTo("access-token"));
    }

    [Test]
    public async Task Refresh_DelegatesToServiceAndReturnsOkWithNewTokens()
    {
        var user = MakeUser();
        _auth.Setup(a => a.RefreshAsync("old-refresh-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeAuthResult(user));

        var result = await _controller.Refresh(new RefreshRequest("old-refresh-token"));

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
    }

    [Test]
    public async Task Logout_DelegatesToServiceAndReturnsNoContent()
    {
        _auth.Setup(a => a.LogoutAsync("a-refresh-token", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Logout(new LogoutRequest("a-refresh-token"));

        Assert.That(result, Is.TypeOf<NoContentResult>());
        _auth.Verify(a => a.LogoutAsync("a-refresh-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Me_ReturnsTheUserIdentifiedByTheAccessTokensSubClaim()
    {
        var user = MakeUser();
        var users = new Mock<IUserService>(MockBehavior.Strict);
        users.Setup(u => u.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        SetAuthenticatedUser(user.Id);

        var result = await _controller.Me(users.Object);

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That((ok!.Value as UserDto)!.Id, Is.EqualTo(user.Id));
    }

    [Test]
    public async Task Me_WhenTheUserNoLongerExists_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var users = new Mock<IUserService>(MockBehavior.Strict);
        users.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        SetAuthenticatedUser(userId);

        var result = await _controller.Me(users.Object);

        Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
    }

    private void SetAuthenticatedUser(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim("sub", userId.ToString())],
            authenticationType: "Test");

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
    }
}
