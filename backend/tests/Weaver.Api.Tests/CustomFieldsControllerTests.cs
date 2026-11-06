using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

[TestFixture]
public class CustomFieldsControllerTests
{
    private Mock<ICustomFieldService> _fields = null!;
    private CustomFieldsController _controller = null!;
    private Guid _userId;
    private Guid _typeId;

    [SetUp]
    public void SetUp()
    {
        _fields = new Mock<ICustomFieldService>(MockBehavior.Strict);
        _userId = Guid.NewGuid();
        _typeId = Guid.NewGuid();
        _controller = new CustomFieldsController(_fields.Object) { ControllerContext = SignedInControllerContext.For(_userId) };
    }

    [Test]
    public async Task Create_PassesTheCallerKindAndOptions_AndReturns201()
    {
        var field = CustomFieldDefinition.Create(_typeId, "Floor", CustomFieldKind.SingleSelect, 0, ["Oak"]);
        _fields.Setup(f => f.CreateAsync(
                _userId, _typeId, "Floor", CustomFieldKind.SingleSelect,
                It.Is<IReadOnlyList<string>>(o => o.SequenceEqual(new[] { "Oak" })),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(field);

        var result = await _controller.Create(new CreateCustomFieldRequest(_typeId, "Floor", CustomFieldKind.SingleSelect, ["Oak"]));

        var created = result.Result as CreatedAtActionResult;
        Assert.Multiple(() =>
        {
            Assert.That(created!.RouteValues!["typeId"], Is.EqualTo(_typeId));
            Assert.That((created.Value as CustomFieldDto)!.Options.Single().Name, Is.EqualTo("Oak"));
        });
    }

    [Test]
    public async Task Create_WithoutOptions_PassesNone()
    {
        var field = CustomFieldDefinition.Create(_typeId, "Budget", CustomFieldKind.Currency, 0);
        _fields.Setup(f => f.CreateAsync(
                _userId, _typeId, "Budget", CustomFieldKind.Currency,
                It.Is<IReadOnlyList<string>>(o => o.Count == 0),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(field);

        await _controller.Create(new CreateCustomFieldRequest(_typeId, "Budget", CustomFieldKind.Currency));

        _fields.VerifyAll();
    }

    [Test]
    public async Task Update_PassesTheOptionRevisions()
    {
        var field = CustomFieldDefinition.Create(_typeId, "Floor", CustomFieldKind.SingleSelect, 0, ["Oak"]);
        var oak = field.Options[0].Id;
        _fields.Setup(f => f.UpdateAsync(
                _userId, field.Id, "Flooring",
                It.Is<IReadOnlyList<CustomFieldOptionRevision>>(o =>
                    o.SequenceEqual(new CustomFieldOptionRevision[] { new(oak, "Oak"), new(null, "Pine") })),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(field);

        var result = await _controller.Update(field.Id, new UpdateCustomFieldRequest("Flooring", [new(oak, "Oak"), new(null, "Pine")]));

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        _fields.VerifyAll();
    }

    [Test]
    public async Task CreateAndUpdate_PassWhetherTheFieldRollsUp()
    {
        var field = CustomFieldDefinition.Create(_typeId, "Hours", CustomFieldKind.Number, 0, rollsUp: true);
        _fields.Setup(f => f.CreateAsync(
                _userId, _typeId, "Hours", CustomFieldKind.Number, It.IsAny<IReadOnlyList<string>>(), true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(field);
        _fields.Setup(f => f.UpdateAsync(
                _userId, field.Id, "Hours", It.IsAny<IReadOnlyList<CustomFieldOptionRevision>>(), true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(field);

        var created = await _controller.Create(new CreateCustomFieldRequest(_typeId, "Hours", CustomFieldKind.Number, RollsUp: true));
        await _controller.Update(field.Id, new UpdateCustomFieldRequest("Hours", RollsUp: true));

        Assert.That(((created.Result as CreatedAtActionResult)!.Value as CustomFieldDto)!.RollsUp, Is.True);
        _fields.VerifyAll();
    }

    [Test]
    public async Task GetAllInSpace_ListsTheSpacesFields()
    {
        var spaceId = Guid.NewGuid();
        var field = CustomFieldDefinition.Create(_typeId, "Hours", CustomFieldKind.Number, 0);
        _fields.Setup(f => f.GetAllInSpaceAsync(spaceId, It.IsAny<CancellationToken>())).ReturnsAsync([field]);

        var result = await _controller.GetAllInSpace(spaceId);

        var dtos = (result.Result as OkObjectResult)!.Value as IEnumerable<CustomFieldDto>;
        Assert.That(dtos!.Single().Id, Is.EqualTo(field.Id));
    }

    [Test]
    public async Task Delete_PassesTheCaller_AndReturns204()
    {
        var id = Guid.NewGuid();
        _fields.Setup(f => f.DeleteAsync(_userId, id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.Delete(id);

        Assert.That(result, Is.TypeOf<NoContentResult>());
    }
}
