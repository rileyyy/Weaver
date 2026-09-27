using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Weaver.Api.Contracts;

namespace Weaver.Api.IntegrationTests;

[TestFixture]
public class RestConventionTests
{
    [Test]
    public async Task CreatingAComment_Returns201WithALocationThatResolves()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var item = await client.CreateWorkItemAsync("Commented");

        var created = await client.PostJsonAsync($"/api/work-items/{item.Id}/comments", new CreateCommentRequest("Hi"));
        var fetched = await (await client.GetAsync(created.Headers.Location)).ReadAsync<CommentDto>();

        Assert.Multiple(() =>
        {
            Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(fetched.Body, Is.EqualTo("Hi"));
        });
    }

    [Test]
    public async Task CreatingALink_Returns201()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var a = await client.CreateWorkItemAsync("A");
        var b = await client.CreateWorkItemAsync("B");

        var response = await client.PostJsonAsync($"/api/work-items/{a.Id}/links", new CreateWorkItemLinkRequest(b.Id));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task FieldUpdates_ArePuts()
    {
        var (client, _) = await ApiClient.SignedInAsync();
        var item = await client.CreateWorkItemAsync("Moved");

        var put = await client.PutJsonAsync(
            $"/api/work-items/{item.Id}/status",
            new ChangeWorkItemStatusRequest(SeededStatuses.Doing, null));
        var post = await client.PostJsonAsync(
            $"/api/work-items/{item.Id}/status",
            new ChangeWorkItemStatusRequest(SeededStatuses.Doing, null));

        Assert.Multiple(() =>
        {
            Assert.That(put.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(post.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
        });
    }

    [Test]
    public async Task OpenApi_DescribesTheErrorShapes()
    {
        // The OpenAPI document is only mapped in Development, and sits behind the same
        // authentication as everything else.
        var (_, auth) = await ApiClient.SignedInAsync();
        await using var factory = IntegrationTestEnvironment.Factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment("Development"));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var document = await client.GetStringAsync("/openapi/v1.json");

        Assert.Multiple(() =>
        {
            Assert.That(document, Does.Contain("application/problem+json"));
            Assert.That(document, Does.Contain("\"409\""));
            Assert.That(document, Does.Contain("\"201\""));
        });
    }
}
