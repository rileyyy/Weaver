using Weaver.Domain.Exceptions;

namespace Weaver.Application.Services;

/// <summary>The offset and limit rules every paged list shares; each list sets its own largest page.</summary>
public static class PagingGuard
{
    public static void EnsureValid(int offset, int limit, int maxPageSize)
    {
        if (offset < 0)
        {
            throw new DomainValidationException("Offset must not be negative.");
        }

        if (limit < 1 || limit > maxPageSize)
        {
            throw new DomainValidationException($"Limit must be between 1 and {maxPageSize}.");
        }
    }
}
