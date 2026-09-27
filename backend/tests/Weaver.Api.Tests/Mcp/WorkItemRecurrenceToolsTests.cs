using ModelContextProtocol;
using Moq;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class WorkItemRecurrenceToolsTests
{
    private Mock<IWorkItemRecurrenceService> _recurrences = null!;
    private WorkItemRecurrenceTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        _recurrences = new Mock<IWorkItemRecurrenceService>(MockBehavior.Strict);
        _tools = new WorkItemRecurrenceTools(_recurrences.Object);
    }

    [Test]
    public async Task ListRepeatingWorkItems_DelegatesToServiceAndMapsResults()
    {
        var id = Guid.NewGuid();
        _recurrences.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([WorkItemRecurrencesControllerTests.MakeRecurring(id)]);

        var result = await _tools.ListRepeatingWorkItems(CancellationToken.None);

        Assert.That(result.Single().WorkItemId, Is.EqualTo(id));
    }

    [Test]
    public async Task GetWorkItemRecurrence_WhenNotRepeating_ReturnsNull()
    {
        var id = Guid.NewGuid();
        _recurrences.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((RecurringWorkItem?)null);

        Assert.That(await _tools.GetWorkItemRecurrence(id, CancellationToken.None), Is.Null);
    }

    [Test]
    public void GetWorkItemRecurrence_WithUnknownWorkItem_ThrowsMcpExceptionWithSameMessage()
    {
        var id = Guid.NewGuid();
        var domainException = new EntityNotFoundException(nameof(WorkItem), id);
        _recurrences.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>())).ThrowsAsync(domainException);

        var thrown = Assert.ThrowsAsync<McpException>(() => _tools.GetWorkItemRecurrence(id, CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    [Test]
    public async Task SetWorkItemRecurrence_WithNoDays_PassesAnEmptyList()
    {
        var id = Guid.NewGuid();
        var start = new DateOnly(2026, 9, 28);
        _recurrences
            .Setup(r => r.SetAsync(
                id, RecurrenceFrequency.Yearly, It.Is<IReadOnlyCollection<DayOfWeek>>(d => d.Count == 0),
                start, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkItemRecurrencesControllerTests.MakeRecurring(id));

        var result = await _tools.SetWorkItemRecurrence(id, RecurrenceFrequency.Yearly, null, start, null, CancellationToken.None);

        Assert.That(result.WorkItemId, Is.EqualTo(id));
    }

    [Test]
    public void SetWorkItemRecurrence_WhenInvalid_ThrowsMcpExceptionWithSameMessage()
    {
        var id = Guid.NewGuid();
        var domainException = new DomainValidationException("Choose at least one day of the week to repeat on.");
        _recurrences
            .Setup(r => r.SetAsync(
                id, It.IsAny<RecurrenceFrequency>(), It.IsAny<IReadOnlyCollection<DayOfWeek>>(),
                It.IsAny<DateOnly>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(domainException);

        var thrown = Assert.ThrowsAsync<McpException>(() => _tools.SetWorkItemRecurrence(
            id, RecurrenceFrequency.Weekly, [], new DateOnly(2026, 9, 28), null, CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    [Test]
    public async Task RemoveWorkItemRecurrence_DelegatesToService()
    {
        var id = Guid.NewGuid();
        _recurrences.Setup(r => r.RemoveAsync(id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _tools.RemoveWorkItemRecurrence(id, CancellationToken.None);

        _recurrences.VerifyAll();
    }
}
