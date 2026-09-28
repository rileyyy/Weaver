using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Weaver.Api.Contracts;
using Weaver.Infrastructure;

namespace Weaver.Api.IntegrationTests;

/// <summary>Helpers for talking to the API the way the Flutter client does.</summary>
public static class ApiClient
{
    public const string Password = "integration-test-password";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static HttpClient Anonymous() => IntegrationTestEnvironment.Factory.CreateClient();

    public static string UniqueUsername() => $"user-{Guid.NewGuid():N}"[..30];

    /// <summary>Registers a fresh user and returns a client that sends their access token.</summary>
    public static async Task<(HttpClient Client, AuthResponse Auth)> SignedInAsync()
    {
        var client = Anonymous();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(UniqueUsername(), Password),
            Json);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    /// <summary>
    /// A fresh user promoted to admin straight in the database: only the first account on a
    /// database is an admin, and which test registers first isn't fixed.
    /// </summary>
    public static async Task<(HttpClient Client, AuthResponse Auth)> SignedInAdminAsync()
    {
        var (client, auth) = await SignedInAsync();
        using var scope = IntegrationTestEnvironment.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaverDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == auth.User.Id);
        user.GrantAdmin();
        await db.SaveChangesAsync();
        return (client, auth);
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string path, T body) =>
        client.PostAsJsonAsync(path, body, Json);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string path, T body) =>
        client.PutAsJsonAsync(path, body, Json);

    public static async Task<WorkItemDto> CreateWorkItemAsync(
        this HttpClient client,
        string title,
        Guid? parentId = null) =>
        await (await client.PostJsonAsync(
            "/api/work-items",
            new CreateWorkItemRequest(title, null, parentId, SeededStatuses.ToDo, null))).ReadAsync<WorkItemDto>();
}
