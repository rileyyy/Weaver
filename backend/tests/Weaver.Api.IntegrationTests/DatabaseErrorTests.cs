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
    public async Task ConcurrentRegistrations_OfOneName_NeverReturn500()
    {
        var name = ApiClient.UniqueUsername();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            ApiClient.Anonymous().PostJsonAsync("/api/auth/register", new RegisterRequest(name, ApiClient.Password))));

        Assert.Multiple(() =>
        {
            Assert.That(responses.Count(r => r.StatusCode == HttpStatusCode.OK), Is.EqualTo(1));
            Assert.That(
                responses.Where(r => r.StatusCode != HttpStatusCode.OK).Select(r => r.StatusCode),
                Is.All.EqualTo(HttpStatusCode.Conflict));
        });
    }

    [Test]
    public async Task Errors_AreSentAsProblemJson()
    {
        var (client, _) = await ApiClient.SignedInAsync();

        var notFound = await client.PostJsonAsync(
            $"/api/work-items/{Guid.NewGuid()}/assignee",
            new AssignWorkItemRequest(null));

        Assert.That(notFound.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
    }

    private static User NewUser(string name) => User.CreateHuman(name, name);
}
