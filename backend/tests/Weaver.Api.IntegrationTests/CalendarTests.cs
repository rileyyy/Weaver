using System.Text.Json;

namespace Weaver.Api.IntegrationTests;

/// <summary>The server's today over REST, in the shape the app's Agenda reads.</summary>
[TestFixture]
public class CalendarTests
{
    [Test]
    public async Task Today_IsAPlainDateNearTheServersClock()
    {
        var (client, _) = await ApiClient.SignedInAsync();

        var response = await client.GetAsync("/api/calendar/today");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var today = DateOnly.ParseExact(json.RootElement.GetProperty("today").GetString()!, "yyyy-MM-dd");

        // Other tests may set any time zone, which puts today within a day of UTC's.
        var utcToday = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.That(today, Is.InRange(utcToday.AddDays(-1), utcToday.AddDays(1)));
    }
}
