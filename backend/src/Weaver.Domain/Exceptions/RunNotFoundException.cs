namespace Weaver.Domain.Exceptions;

/// <summary>No agent's history entry carries this run id from this account and key.</summary>
public class RunNotFoundException(string runId, Guid accountId, Guid apiKeyId)
    : DomainException(DomainErrorKind.NotFound, $"Run '{runId}' of account {accountId} and key {apiKeyId} was not found.")
{
}
