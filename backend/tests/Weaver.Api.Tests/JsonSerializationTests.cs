using System.Text.Json;
using System.Text.Json.Serialization;
using Weaver.Api.Contracts;
using Weaver.Api.Json;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests;

/// <summary>
/// Documents a real bug found manually verifying auth end to end: the
/// frontend parses DTO enum fields (e.g. <c>UserDto.Kind</c>) as strings, so
/// serializing them as raw ints — <see cref="JsonSerializer"/>'s default —
/// silently threw client-side despite the API call itself succeeding.
/// Program.cs registers a global <c>JsonStringEnumConverter</c> to fix this;
/// these tests only pin down the *contract* (enum DTO fields must serialize
/// as their string name) using that same converter explicitly. They don't
/// exercise Program.cs's registration itself — this codebase has no
/// integration-test harness (WebApplicationFactory) yet, so removing that
/// registration wouldn't be caught here.
/// </summary>
[TestFixture]
public class JsonSerializationTests
{
    // Mirrors ASP.NET Core's default controller serialization (camelCase
    // property names), not JsonSerializer's own bare defaults (PascalCase) —
    // otherwise this test would pass/fail on the wrong thing.
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter(), new OptionalJsonConverterFactory() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Test]
    public void UserDto_SerializesKindAsAString()
    {
        var dto = new UserDto(Guid.NewGuid(), "alice", UserKind.Agent, IsAdmin: false);

        var json = JsonSerializer.Serialize(dto, Options);

        Assert.That(json, Does.Contain("\"kind\":\"Agent\""));
    }

    [Test]
    public void StatusDto_SerializesCategoryAsAString()
    {
        var dto = new StatusDto(Guid.NewGuid(), Guid.NewGuid(), "Doing", 1, StatusCategory.Doing, "#FB8C00");

        var json = JsonSerializer.Serialize(dto, Options);

        Assert.That(json, Does.Contain("\"category\":\"Doing\""));
    }

    [Test]
    public void WorkItemDto_SerializesScheduleDatesAsCalendarDates()
    {
        var item = TestWorkItems.Create("Item");
        item.Reschedule(new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 30));

        var json = JsonSerializer.Serialize(WorkItemDto.FromEntity(item, blockers: [], WorkItemNames.None), Options);

        Assert.That(json, Does.Contain("\"startDate\":\"2026-09-25\""));
        Assert.That(json, Does.Contain("\"endDate\":\"2026-09-30\""));
    }

    [Test]
    public void WorkItemDto_SerializesTheCompletionDateAsACalendarDate_OrNullWhileNotDone()
    {
        var done = TestWorkItems.Create("Done", StatusCategory.Done);
        var open = TestWorkItems.Create("Open");

        var doneJson = JsonSerializer.Serialize(WorkItemDto.FromEntity(done, blockers: [], WorkItemNames.None), Options);
        var openJson = JsonSerializer.Serialize(WorkItemDto.FromEntity(open, blockers: [], WorkItemNames.None), Options);

        Assert.Multiple(() =>
        {
            Assert.That(doneJson, Does.Contain($"\"completionDate\":\"{TestWorkItems.Today:yyyy-MM-dd}\""));
            Assert.That(openJson, Does.Contain("\"completionDate\":null"));
        });
    }

    [Test]
    public void WorkItemDto_SerializesEachBlockerWithItsEndDateAsACalendarDate()
    {
        var item = TestWorkItems.Create("Paint");
        var blocker = new WorkItemBlocker(Guid.NewGuid(), 4, "Drywall", Guid.NewGuid(), new DateOnly(2026, 11, 6), true);

        var json = JsonSerializer.Serialize(WorkItemDto.FromEntity(item, [blocker], WorkItemNames.None), Options);

        Assert.That(json, Does.Contain(
            $"\"blockedBy\":[{{\"id\":\"{blocker.Id}\",\"number\":4,\"title\":\"Drywall\",\"spaceId\":\"{blocker.SpaceId}\",\"endDate\":\"2026-11-06\",\"conflicts\":true}}]"));
    }

    [Test]
    public void WorkItemDto_WithNoBlockers_SerializesAnEmptyBlockedByList()
    {
        var json = JsonSerializer.Serialize(WorkItemDto.FromEntity(TestWorkItems.Create("Paint"), blockers: [], WorkItemNames.None), Options);

        Assert.That(json, Does.Contain("\"blockedBy\":[]"));
    }

    [Test]
    public void WorkItemSummaryDto_CarriesTheCompletionDate()
    {
        var summary = new WorkItemSummary(
            Guid.NewGuid(), 3, "Fence", Guid.NewGuid(), "Home", null, "Done", null, null, null, null, 0,
            CompletionDate: new DateOnly(2026, 11, 2));

        var json = JsonSerializer.Serialize(WorkItemSummaryDto.FromSummary(summary), Options);

        Assert.That(json, Does.Contain("\"completionDate\":\"2026-11-02\""));
    }

    [Test]
    public void SpaceDto_CarriesTheDoneHidingPeriod()
    {
        var space = Space.Create("Home");

        var json = JsonSerializer.Serialize(SpaceDto.FromEntity(space), Options);

        Assert.That(json, Is.EqualTo($$"""{"id":"{{space.Id}}","name":"Home","doneHidingDays":14}"""));
    }

    [Test]
    public void RescheduleWorkItemRequest_ReadsCalendarDates()
    {
        var request = JsonSerializer.Deserialize<RescheduleWorkItemRequest>(
            """{"startDate":"2026-09-25","endDate":null}""", Options);

        Assert.That(request!.StartDate.Value, Is.EqualTo(new DateOnly(2026, 9, 25)));
        Assert.That(request.EndDate, Is.EqualTo(new Optional<DateOnly?>(null)));
    }

    [Test]
    public void UpdateWorkItemDetailsRequest_TellsALeftOutFieldFromANullOne()
    {
        var request = JsonSerializer.Deserialize<UpdateWorkItemDetailsRequest>(
            """{"title":"Renamed","description":null,"expectedVersion":7}""", Options);

        Assert.Multiple(() =>
        {
            Assert.That(request!.Title, Is.EqualTo(new Optional<string>("Renamed")));
            Assert.That(request.Description, Is.EqualTo(new Optional<string?>(null)));
            Assert.That(request.TypeId.HasValue, Is.False);
            Assert.That(request.PriorityId.HasValue, Is.False);
            Assert.That(request.ExpectedVersion, Is.EqualTo(7u));
        });
    }

    [Test]
    public void SetTagsWorkItemRequest_TellsALeftOutListFromANullOne()
    {
        var leftOut = JsonSerializer.Deserialize<SetTagsWorkItemRequest>("""{}""", Options);
        var cleared = JsonSerializer.Deserialize<SetTagsWorkItemRequest>("""{"tags":null}""", Options);

        Assert.Multiple(() =>
        {
            Assert.That(leftOut!.Tags.HasValue, Is.False);
            Assert.That(cleared!.Tags.HasValue, Is.True);
            Assert.That(cleared.Tags.Value, Is.Null);
        });
    }

    [Test]
    public void WorkItemRecurrenceDto_SerializesFrequencyAndDaysAsStrings()
    {
        var dto = new WorkItemRecurrenceDto(
            Guid.NewGuid(), 7, "Report", null, null, RecurrenceFrequency.BiWeekly,
            [DayOfWeek.Monday, DayOfWeek.Friday], new DateOnly(2026, 9, 28), null, new DateOnly(2026, 9, 28));

        var json = JsonSerializer.Serialize(dto, Options);

        Assert.That(json, Does.Contain("\"frequency\":\"BiWeekly\""));
        Assert.That(json, Does.Contain("\"daysOfWeek\":[\"Monday\",\"Friday\"]"));
        Assert.That(json, Does.Contain("\"startDate\":\"2026-09-28\""));
    }

    [Test]
    public void SetWorkItemRecurrenceRequest_ReadsStringEnumsAndDates()
    {
        var request = JsonSerializer.Deserialize<SetWorkItemRecurrenceRequest>(
            """{"frequency":"Weekly","daysOfWeek":["Sunday"],"startDate":"2026-09-28","endDate":null}""", Options);

        Assert.That(request!.Frequency, Is.EqualTo(RecurrenceFrequency.Weekly));
        Assert.That(request.DaysOfWeek, Is.EqualTo(new[] { DayOfWeek.Sunday }));
        Assert.That(request.EndDate, Is.Null);
    }
}
