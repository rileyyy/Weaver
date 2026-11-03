namespace Weaver.Domain.Exceptions;

/// <summary>
/// A work item can only use its own Space's statuses and priorities (ADR-0001), so naming one
/// from another Space is refused rather than silently mixing configurations.
/// </summary>
public class LookupNotInSpaceException(string kind, Guid valueId, Guid spaceId) : DomainException(
    DomainErrorKind.Validation,
    $"{kind} {valueId} belongs to another Space; use one of Space {spaceId}'s {kind.ToLowerInvariant()} values.")
{
}
