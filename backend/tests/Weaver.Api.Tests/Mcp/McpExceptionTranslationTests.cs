using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using Weaver.Api.Middleware;
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

    [Test]
    public void TranslateAsync_WhenOperationThrowsWorkItemIsBoardScopeException_ThrowsMcpException()
    {
        var domainException = new WorkItemIsBoardScopeException(Guid.NewGuid(), Guid.NewGuid());

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            McpExceptionTranslation.TranslateAsync(() => throw domainException));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    [Test]
    public void TranslateAsync_WhenOperationThrowsDomainValidationException_ThrowsMcpException()
    {
        var domainException = new DomainValidationException("Title is required.");

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            McpExceptionTranslation.TranslateAsync(() => throw domainException));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    [Test]
    public void TranslateAsync_CoversEveryDomainException_WithoutAListToMaintain()
    {
        var domainException = new UniqueConstraintViolationException("ix_users_normalized_username", new Exception());

        var thrown = Assert.ThrowsAsync<McpException>(() =>
            McpExceptionTranslation.TranslateAsync(() => throw domainException));

        Assert.That(thrown!.Message, Is.EqualTo(domainException.Message));
    }

    [Test]
    public void TranslateAsync_WhenAConcurrencyConflictEscapes_ThrowsMcpException()
    {
        var thrown = Assert.ThrowsAsync<McpException>(() =>
            McpExceptionTranslation.TranslateAsync(() => throw new DbUpdateConcurrencyException("stale")));

        Assert.That(thrown!.Message, Is.EqualTo(ApiExceptionMiddleware.ConcurrencyConflictMessage));
    }
}
