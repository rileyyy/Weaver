using System.Text.Json;
using System.Text.Json.Serialization;
using Weaver.Api.Contracts;
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
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Test]
    public void UserDto_SerializesKindAsAString()
    {
        var dto = new UserDto(Guid.NewGuid(), "alice", UserKind.Agent);

        var json = JsonSerializer.Serialize(dto, Options);

        Assert.That(json, Does.Contain("\"kind\":\"Agent\""));
    }

    [Test]
    public void StatusDto_SerializesCategoryAsAString()
    {
        var dto = new StatusDto(Guid.NewGuid(), "Doing", 1, StatusCategory.Doing, "#FB8C00");

        var json = JsonSerializer.Serialize(dto, Options);

        Assert.That(json, Does.Contain("\"category\":\"Doing\""));
    }

    [Test]
    public void WorkItemDto_SerializesScheduleDatesAsCalendarDates()
    {
        var item = new WorkItem
        {
            Id = Guid.NewGuid(),
            Title = "Item",
            StartDate = new DateOnly(2026, 9, 25),
            EndDate = new DateOnly(2026, 9, 30),
        };

        var json = JsonSerializer.Serialize(WorkItemDto.FromEntity(item), Options);

        Assert.That(json, Does.Contain("\"startDate\":\"2026-09-25\""));
        Assert.That(json, Does.Contain("\"endDate\":\"2026-09-30\""));
    }

    [Test]
    public void RescheduleWorkItemRequest_ReadsCalendarDates()
    {
        var request = JsonSerializer.Deserialize<RescheduleWorkItemRequest>(
            """{"startDate":"2026-09-25","endDate":null}""", Options);

        Assert.That(request!.StartDate, Is.EqualTo(new DateOnly(2026, 9, 25)));
        Assert.That(request.EndDate, Is.Null);
    }
}
