using System.Net;
using Weaver.Api.Contracts;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// The seeded settings row, the singleton table and the admin gate over real HTTP. Every test
/// that changes the time zone puts UTC back, since every test shares the database.
/// </summary>
[TestFixture]
public class ServerSettingsTests
{
    private const string Path = "/api/server-settings";
    private const string TimeZonePath = "/api/server-settings/time-zone";

    [Test]
    public async Task ServerSettings_AreReadableByAnyoneSignedIn_AndDefaultToUtc()
    {
        var (client, _) = await ApiClient.SignedInAsync();

        var settings = await (await client.GetAsync(Path)).ReadAsync<ServerSettingsDto>();

        Assert.That(settings.TimeZoneId, Is.EqualTo("UTC"));
    }

    [Test]
    public async Task SettingTheTimeZone_AsAnAdmin_IsStored()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        try
        {
            var updated = await (await admin.PutJsonAsync(TimeZonePath, new SetTimeZoneRequest("Europe/Berlin")))
                .ReadAsync<ServerSettingsDto>();
            var stored = await (await admin.GetAsync(Path)).ReadAsync<ServerSettingsDto>();

            Assert.Multiple(() =>
            {
                Assert.That(updated.TimeZoneId, Is.EqualTo("Europe/Berlin"));
                Assert.That(stored.TimeZoneId, Is.EqualTo("Europe/Berlin"));
            });
        }
        finally
        {
            await admin.PutJsonAsync(TimeZonePath, new SetTimeZoneRequest("UTC"));
        }
    }

    [Test]
    public async Task SettingTheTimeZone_AsANonAdmin_Returns403()
    {
        var (client, _) = await ApiClient.SignedInAsync();

        var response = await client.PutJsonAsync(TimeZonePath, new SetTimeZoneRequest("Europe/Berlin"));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        });
    }

    [Test]
    public async Task SettingAnUnknownTimeZone_Returns400()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();

        var response = await admin.PutJsonAsync(TimeZonePath, new SetTimeZoneRequest("Mars/Olympus_Mons"));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        });
    }
}
