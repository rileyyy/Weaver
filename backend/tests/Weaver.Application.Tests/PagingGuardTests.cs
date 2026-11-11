using Weaver.Application.Services;
using Weaver.Domain.Exceptions;

namespace Weaver.Application.Tests;

[TestFixture]
public class PagingGuardTests
{
    [TestCase(0, 1)]
    [TestCase(40, 10)]
    public void AnOffsetFromZero_AndALimitFromOneToTheMax_AreAccepted(int offset, int limit)
    {
        Assert.DoesNotThrow(() => PagingGuard.EnsureValid(offset, limit, maxPageSize: 10));
    }

    [Test]
    public void ANegativeOffset_IsRejected()
    {
        Assert.Throws<DomainValidationException>(() => PagingGuard.EnsureValid(-1, 5, maxPageSize: 10));
    }

    [TestCase(0)]
    [TestCase(11)]
    public void ALimitOutOfRange_IsRejected_NamingTheMax(int limit)
    {
        var error = Assert.Throws<DomainValidationException>(() => PagingGuard.EnsureValid(0, limit, maxPageSize: 10));

        Assert.That(error!.Message, Is.EqualTo("Limit must be between 1 and 10."));
    }
}
