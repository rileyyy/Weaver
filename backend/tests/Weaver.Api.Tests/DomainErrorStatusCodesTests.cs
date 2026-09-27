using Microsoft.AspNetCore.Http;
using Weaver.Api.Middleware;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Tests;

[TestFixture]
public class DomainErrorStatusCodesTests
{
    [Test]
    public void EveryKind_HasAStatus()
    {
        foreach (var kind in Enum.GetValues<DomainErrorKind>())
        {
            Assert.DoesNotThrow(() => DomainErrorStatusCodes.For(kind), kind.ToString());
        }
    }

    [TestCase(DomainErrorKind.NotFound, StatusCodes.Status404NotFound)]
    [TestCase(DomainErrorKind.Validation, StatusCodes.Status400BadRequest)]
    [TestCase(DomainErrorKind.Conflict, StatusCodes.Status409Conflict)]
    [TestCase(DomainErrorKind.Unauthorized, StatusCodes.Status401Unauthorized)]
    [TestCase(DomainErrorKind.Forbidden, StatusCodes.Status403Forbidden)]
    public void Kind_MapsToItsStatus(DomainErrorKind kind, int expected)
    {
        Assert.That(DomainErrorStatusCodes.For(kind), Is.EqualTo(expected));
    }
}
