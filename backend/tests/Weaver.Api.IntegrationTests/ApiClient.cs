using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Weaver.Api.Contracts;
using Weaver.Domain;

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

    /// <summary>
    /// A fresh account created by the configured first admin, the way every account is made,
    /// and a client that sends its access token.
    /// </summary>
    public static Task<(HttpClient Client, AuthResponse Auth)> SignedInAsync() => CreateAndSignInAsync(isAdmin: false);

    public static Task<(HttpClient Client, AuthResponse Auth)> SignedInAdminAsync() => CreateAndSignInAsync(isAdmin: true);

    /// <summary>Signs in as the admin the test host creates on its empty database.</summary>
    public static async Task<HttpClient> FirstAdminAsync()
    {
        var (client, _) = await SignInAsync(WeaverApiFactory.FirstAdminUsername, WeaverApiFactory.FirstAdminPassword);
        return client;
    }

    /// <summary>A client that signs every request with an agent's API key, as an MCP client does.</summary>
    public static HttpClient WithApiKey(string key)
    {
        var client = Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return client;
    }

    /// <summary>
    /// A fresh agent account, created by the first admin, and a client that signs every
    /// request with a newly issued key of the given scope.
    /// </summary>
    public static async Task<(HttpClient Client, AgentAccountDto Agent)> SignedInAgentAsync(ApiKeyScope scope)
    {
        var admin = await FirstAdminAsync();
        var agent = await (await admin.PostJsonAsync(
                "/api/admin/agents",
                new CreateAgentAccountRequest($"Agent ({Guid.NewGuid():N})"[..40], null)))
            .ReadAsync<AgentAccountDto>();
        var issued = await (await admin.PostJsonAsync(
                $"/api/admin/agents/{agent.Id}/keys",
                new CreateApiKeyRequest("test", scope)))
            .ReadAsync<IssuedApiKeyDto>();
        return (WithApiKey(issued.Key), agent);
    }

    public static async Task<(HttpClient Client, AuthResponse Auth)> SignInAsync(string username, string password)
    {
        var client = Anonymous();
        var auth = await (await client.PostJsonAsync("/api/auth/login", new LoginRequest(username, password)))
            .ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private static async Task<(HttpClient Client, AuthResponse Auth)> CreateAndSignInAsync(bool isAdmin)
    {
        var admin = await FirstAdminAsync();
        var username = UniqueUsername();
        var created = await admin.PostJsonAsync("/api/admin/users", new CreateUserRequest(username, Password, isAdmin));
        created.EnsureSuccessStatusCode();
        return await SignInAsync(username, Password);
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

    /// <summary>
    /// Patch bodies are usually anonymous objects, so a test states exactly which fields it
    /// sends: a property it leaves out is absent from the JSON, and a null one is sent as null.
    /// </summary>
    public static Task<HttpResponseMessage> PatchJsonAsync<T>(this HttpClient client, string path, T body) =>
        client.PatchAsJsonAsync(path, body, Json);

    /// <summary>
    /// A top-level item goes in <paramref name="spaceId"/>, or the run's shared Space when
    /// that's omitted; a child goes in its parent's. Either way it starts in that Space's To Do.
    /// </summary>
    public static async Task<WorkItemDto> CreateWorkItemAsync(
        this HttpClient client,
        string title,
        Guid? parentId = null,
        Guid? spaceId = null)
    {
        var space = parentId is null ? spaceId ?? await SharedSpaceIdAsync() : (Guid?)null;
        var toDo = await StatusIdAsync(space ?? await client.SpaceIdOfAsync(parentId!.Value), "To Do");
        return await (await client.PostJsonAsync(
            "/api/work-items",
            new CreateWorkItemRequest(title, null, parentId, toDo, null, SpaceId: space))).ReadAsync<WorkItemDto>();
    }

    public static async Task<Guid> SpaceIdOfAsync(this HttpClient client, Guid workItemId) =>
        (await (await client.GetAsync($"/api/work-items/{workItemId}")).ReadAsync<WorkItemDto>()).SpaceId;

    /// <summary>
    /// The id of <paramref name="spaceId"/>'s status called <paramref name="name"/>, such as one
    /// of the defaults every new Space gets. Looked up once per Space, so it can't see a status
    /// added later.
    /// </summary>
    public static async Task<Guid> StatusIdAsync(Guid spaceId, string name) =>
        (await StatusIdsBySpace.GetOrAdd(spaceId, id => LookupIdsByNameAsync<StatusDto>(
            $"/api/statuses?spaceId={id}", s => (s.Name, s.Id))))[name];

    /// <summary>As <see cref="StatusIdAsync"/>, for priorities.</summary>
    public static async Task<Guid> PriorityIdAsync(Guid spaceId, string name) =>
        (await PriorityIdsBySpace.GetOrAdd(spaceId, id => LookupIdsByNameAsync<WorkItemPriorityDto>(
            $"/api/work-item-priorities?spaceId={id}", p => (p.Name, p.Id))))[name];

    private static readonly ConcurrentDictionary<Guid, Task<Dictionary<string, Guid>>> StatusIdsBySpace = new();

    private static readonly ConcurrentDictionary<Guid, Task<Dictionary<string, Guid>>> PriorityIdsBySpace = new();

    private static async Task<Dictionary<string, Guid>> LookupIdsByNameAsync<TDto>(
        string path,
        Func<TDto, (string Name, Guid Id)> nameAndId)
    {
        var admin = await FirstAdminAsync();
        var values = await (await admin.GetAsync(path)).ReadAsync<List<TDto>>();
        return values.Select(nameAndId).ToDictionary(v => v.Name, v => v.Id);
    }

    /// <summary>
    /// The Space most tests put their items in. The database starts with none, and one per test
    /// would only slow the run: tests already isolate themselves by their own items.
    /// </summary>
    public static Task<Guid> SharedSpaceIdAsync() => SharedSpace.Value;

    private static readonly Lazy<Task<Guid>> SharedSpace = new(() => CreateSpaceAsync("Integration tests"));

    /// <summary>A new Space, created by the first admin, with a unique name built from <paramref name="name"/>.</summary>
    public static async Task<Guid> CreateSpaceAsync(string name)
    {
        var admin = await FirstAdminAsync();
        var space = await (await admin.PostJsonAsync(
            "/api/spaces",
            new SpaceNameRequest($"{name} {Guid.NewGuid():N}"))).ReadAsync<SpaceDto>();
        return space.Id;
    }
}
