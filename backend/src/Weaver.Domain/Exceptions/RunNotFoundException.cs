namespace Weaver.Domain.Exceptions;

/// <summary>No history entry carries this run id from this account and key.</summary>
public class RunNotFoundException(string runId, Guid accountId)
    : DomainException(DomainErrorKind.NotFound, $"Run '{runId}' of account {accountId} was not found.")
{
}
