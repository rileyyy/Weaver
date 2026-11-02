using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

[TestFixture]
public class ServerSettingsControllerTests
{
    private Mock<IServerSettingsService> _settings = null!;
    private ServerSettingsController _controller = null!;
    private Guid _userId;

    [SetUp]
    public void SetUp()
    {
        _settings = new Mock<IServerSettingsService>(MockBehavior.Strict);
        _userId = Guid.NewGuid();
        _controller = new ServerSettingsController(_settings.Object)
        {
            ControllerContext = SignedInControllerContext.For(_userId),
        };
    }

    [Test]
    public async Task Get_ReturnsTheTimeZone()
    {
        _settings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ServerSettings.CreateDefault());

        var result = await _controller.Get();

        Assert.That((result.Result as OkObjectResult)!.Value, Is.EqualTo(new ServerSettingsDto("UTC")));
    }

    [Test]
    public async Task SetTimeZone_PassesTheCallerAndReturnsTheUpdatedSettings()
    {
        var updated = ServerSettings.CreateDefault();
        updated.SetTimeZone("Europe/Berlin");
        _settings.Setup(s => s.SetTimeZoneAsync(_userId, "Europe/Berlin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        var result = await _controller.SetTimeZone(new SetTimeZoneRequest("Europe/Berlin"));

        Assert.That((result.Result as OkObjectResult)!.Value, Is.EqualTo(new ServerSettingsDto("Europe/Berlin")));
    }
}
