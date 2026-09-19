using ModelContextProtocol;
using Weaver.Api.Mcp;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class McpExceptionTranslationTests
{
    [Test]
    public void TranslateAsyncOfT_WhenOperationThrowsKnownDomainException_ThrowsMcpExceptionWithSameMessage()
    {
        var domainException = new EntityNotFoundException("WorkItem", Guid.NewGuid());

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            McpExceptionTranslation.TranslateAsync<string>(() => throw domainException));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
        Assert.That(thrown.InnerException, Is.SameAs(domainException));
    }

    [Test]
    public void TranslateAsyncOfT_WhenOperationThrowsUnknownException_PropagatesItUnchanged()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            McpExceptionTranslation.TranslateAsync<string>(() => throw new InvalidOperationException("boom")));
    }

    [Test]
    public async Task TranslateAsyncOfT_WhenOperationSucceeds_ReturnsItsResult()
    {
        var result = await McpExceptionTranslation.TranslateAsync(() => Task.FromResult("ok"));

        Assert.That(result, Is.EqualTo("ok"));
    }

    [Test]
    public void TranslateAsync_WhenOperationThrowsKnownDomainException_ThrowsMcpExceptionWithSameMessage()
    {
        var domainException = new WorkItemHasChildrenException(Guid.NewGuid());

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            McpExceptionTranslation.TranslateAsync(() => throw domainException));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }
}
