using System.Net;
using Weaver.Api.Contracts;

namespace Weaver.Api.IntegrationTests;

/// <summary>
/// The seeded settings row, the singleton table and the admin gate over real HTTP. Every test
/// that changes a setting puts its default back, since every test shares the database.
/// </summary>
[TestFixture]
public class ServerSettingsTests
{
    private const string Path = "/api/server-settings";
    private const string TimeZonePath = "/api/server-settings/time-zone";
    private const string TrashRetentionPath = "/api/server-settings/trash-retention";

    [Test]
    public async Task ServerSettings_AreReadableByAnyoneSignedIn_AndDefaultToUtc()
    {
        var (client, _) = await ApiClient.SignedInAsync();

        var settings = await (await client.GetAsync(Path)).ReadAsync<ServerSettingsDto>();

        Assert.Multiple(() =>
        {
            Assert.That(settings.TimeZoneId, Is.EqualTo("UTC"));
            Assert.That(settings.TrashRetentionDays, Is.EqualTo(30));
        });
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

    [Test]
    public async Task SettingTheTrashRetention_AsAnAdmin_IsStored()
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();
        try
        {
            var updated = await (await admin.PutJsonAsync(TrashRetentionPath, new SetTrashRetentionRequest(7)))
                .ReadAsync<ServerSettingsDto>();
            var stored = await (await admin.GetAsync(Path)).ReadAsync<ServerSettingsDto>();

            Assert.Multiple(() =>
            {
                Assert.That(updated.TrashRetentionDays, Is.EqualTo(7));
                Assert.That(stored.TrashRetentionDays, Is.EqualTo(7));
            });
        }
        finally
        {
            await admin.PutJsonAsync(TrashRetentionPath, new SetTrashRetentionRequest(30));
        }
    }

    [Test]
    public async Task SettingTheTrashRetention_AsANonAdmin_Returns403()
    {
        var (client, _) = await ApiClient.SignedInAsync();

        var response = await client.PutJsonAsync(TrashRetentionPath, new SetTrashRetentionRequest(7));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [TestCase(0)]
    [TestCase(3651)]
    public async Task SettingTheTrashRetention_OutOfRange_Returns400(int days)
    {
        var (admin, _) = await ApiClient.SignedInAdminAsync();

        var response = await admin.PutJsonAsync(TrashRetentionPath, new SetTrashRetentionRequest(days));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        });
    }
}
