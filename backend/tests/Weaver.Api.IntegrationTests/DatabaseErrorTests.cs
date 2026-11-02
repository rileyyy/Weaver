using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Weaver.Api.Contracts;
using Weaver.Domain;
using Weaver.Domain.Exceptions;
using Weaver.Infrastructure;

namespace Weaver.Api.IntegrationTests;

[TestFixture]
public class DatabaseErrorTests
{
    [Test]
    public void DuplicateUniqueValue_IsTranslatedToADomainConflict()
    {
        using var scope = IntegrationTestEnvironment.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaverDbContext>();
        var name = ApiClient.UniqueUsername();
        db.Users.AddRange(NewUser(name), NewUser(name));

        var error = Assert.ThrowsAsync<UniqueConstraintViolationException>(() => db.SaveChangesAsync());
        Assert.That(error!.Kind, Is.EqualTo(DomainErrorKind.Conflict));
    }

    [Test]
    public async Task ConcurrentAccountCreations_OfOneName_NeverReturn500()
    {
        var admin = await ApiClient.FirstAdminAsync();
        var request = new CreateUserRequest(ApiClient.UniqueUsername(), ApiClient.Password, IsAdmin: false);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            admin.PostJsonAsync("/api/admin/users", request)));

        Assert.Multiple(() =>
        {
            Assert.That(responses.Count(r => r.StatusCode == HttpStatusCode.Created), Is.EqualTo(1));
            Assert.That(
                responses.Where(r => r.StatusCode != HttpStatusCode.Created).Select(r => r.StatusCode),
                Is.All.EqualTo(HttpStatusCode.Conflict));
        });
    }

    [Test]
    public async Task Errors_AreSentAsProblemJson()
    {
        var (client, _) = await ApiClient.SignedInAsync();

        var notFound = await client.PutJsonAsync(
            $"/api/work-items/{Guid.NewGuid()}/assignee",
            new AssignWorkItemRequest(null));

        Assert.That(notFound.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
    }

    private static User NewUser(string name) => User.CreateHuman(name, name);
}
