namespace Weaver.Domain.Exceptions;

public class BlockingCycleException(Guid blockerId, Guid blockedId) : DomainException(
    DomainErrorKind.Conflict,
    $"Work item {blockedId} already blocks {blockerId}, so {blockerId} can't also block it.")
{
}
