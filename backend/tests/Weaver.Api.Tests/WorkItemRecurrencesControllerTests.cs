using Microsoft.AspNetCore.Mvc;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Tests;

[TestFixture]
public class WorkItemRecurrencesControllerTests
{
    private Mock<IWorkItemRecurrenceService> _recurrences = null!;
    private WorkItemRecurrencesController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _recurrences = new Mock<IWorkItemRecurrenceService>(MockBehavior.Strict);
        _controller = new WorkItemRecurrencesController(_recurrences.Object);
    }

    internal static RecurringWorkItem MakeRecurring(Guid workItemId) => new(
        workItemId,
        12,
        "Report",
        Guid.NewGuid(),
        "Lane",
        RecurrenceSchedule.Create(
            RecurrenceFrequency.Weekly,
            [DayOfWeek.Monday, DayOfWeek.Thursday],
            new DateOnly(2026, 9, 28),
            null),
        new DateOnly(2026, 10, 1));

    [Test]
    public async Task List_MapsEveryRepetition()
    {
        var id = Guid.NewGuid();
        _recurrences.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([MakeRecurring(id)]);

        var result = await _controller.List();

        var body = ((result.Result as OkObjectResult)!.Value as IEnumerable<WorkItemRecurrenceDto>)!.Single();
        Assert.That(body.WorkItemId, Is.EqualTo(id));
        Assert.That(body.WorkItemNumber, Is.EqualTo(12));
        Assert.That(body.ParentTitle, Is.EqualTo("Lane"));
        Assert.That(body.DaysOfWeek, Is.EqualTo(new[] { DayOfWeek.Monday, DayOfWeek.Thursday }));
        Assert.That(body.NextOccurrence, Is.EqualTo(new DateOnly(2026, 10, 1)));
    }

    [Test]
    public async Task Get_WhenRepeating_ReturnsOk()
    {
        var id = Guid.NewGuid();
        _recurrences.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(MakeRecurring(id));

        var result = await _controller.Get(id);

        Assert.That((result.Result as OkObjectResult)!.Value, Is.TypeOf<WorkItemRecurrenceDto>());
    }

    [Test]
    public async Task Get_WhenNotRepeating_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _recurrences.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((RecurringWorkItem?)null);

        var result = await _controller.Get(id);

        Assert.That(result.Result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task Set_DelegatesToServiceAndReturnsOk()
    {
        var id = Guid.NewGuid();
        var start = new DateOnly(2026, 9, 28);
        _recurrences
            .Setup(r => r.SetAsync(
                id, RecurrenceFrequency.Weekly,
                It.Is<IReadOnlyCollection<DayOfWeek>>(d => d.SequenceEqual(new[] { DayOfWeek.Monday })),
                start, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRecurring(id));

        var result = await _controller.Set(
            id, new SetWorkItemRecurrenceRequest(RecurrenceFrequency.Weekly, [DayOfWeek.Monday], start, null));

        Assert.That((result.Result as OkObjectResult)!.Value, Is.TypeOf<WorkItemRecurrenceDto>());
    }

    [Test]
    public async Task Set_WithNoDays_PassesAnEmptyList()
    {
        var id = Guid.NewGuid();
        var start = new DateOnly(2026, 9, 28);
        _recurrences
            .Setup(r => r.SetAsync(
                id, RecurrenceFrequency.Monthly,
                It.Is<IReadOnlyCollection<DayOfWeek>>(d => d.Count == 0),
                start, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRecurring(id));

        var result = await _controller.Set(
            id, new SetWorkItemRecurrenceRequest(RecurrenceFrequency.Monthly, null, start, null));

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
    }

    [Test]
    public void Set_WhenServiceRejectsTheSchedule_PropagatesForTheMiddleware()
    {
        var id = Guid.NewGuid();
        _recurrences
            .Setup(r => r.SetAsync(
                id, It.IsAny<RecurrenceFrequency>(), It.IsAny<IReadOnlyCollection<DayOfWeek>>(),
                It.IsAny<DateOnly>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainValidationException("Choose at least one day of the week to repeat on."));

        Assert.ThrowsAsync<DomainValidationException>(() => _controller.Set(
            id, new SetWorkItemRecurrenceRequest(RecurrenceFrequency.Weekly, [], new DateOnly(2026, 9, 28), null)));
    }

    [Test]
    public async Task Remove_DelegatesToServiceAndReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _recurrences.Setup(r => r.RemoveAsync(id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.Remove(id);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        _recurrences.VerifyAll();
    }
}
